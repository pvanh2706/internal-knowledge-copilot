using System.Diagnostics;
using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Platform;

public sealed class PlatformSecurityPrimitivesTests(ITestOutputHelper output)
{
    private const string Password = "Correct-Horse-Battery-9";

    [Fact]
    public void PasswordHasher_ProducesVersionedPhcArgon2idAcceptedBySchema()
    {
        var hasher = new PlatformPasswordHasher();

        var first = hasher.HashPassword(Password);
        var second = hasher.HashPassword(Password);

        Assert.Matches(new Regex(@"^\$argon2id\$v=19\$m=65536,t=3,p=1\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}$"), first);
        Assert.NotEqual(first, second);
        Assert.True(hasher.VerifyPassword(first, Password));
        Assert.False(hasher.VerifyPassword(first, Password + "x"));

        // Cùng CHECK GLOB với migration S01-A.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT $hash GLOB '$[a-z]*$v=[1-9]*$*$*$*'";
        command.Parameters.AddWithValue("$hash", first);
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=16$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$t=3,m=65536,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=1024,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=4194304,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAA")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=+65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void PasswordHasher_RejectsMalformedUnsupportedOrOutOfBoundsHashesWithoutThrowing(string? storedHash)
    {
        Assert.False(new PlatformPasswordHasher().VerifyPassword(storedHash, Password));
    }

    [Fact]
    public void PasswordHasher_RejectsTenantPbkdf2Hash()
    {
        var tenantHash = new PasswordHasher().HashPassword(Password);

        Assert.True(new PasswordHasher().VerifyPassword(tenantHash, Password));
        Assert.False(new PlatformPasswordHasher().VerifyPassword(tenantHash, Password));
    }

    [Fact]
    public void PasswordHasher_Benchmark()
    {
        var hasher = new PlatformPasswordHasher();
        var hash = hasher.HashPassword(Password);
        hasher.VerifyPassword(hash, Password);

        var hashTimes = Measure(() => hasher.HashPassword(Password));
        var verifyTimes = Measure(() => hasher.VerifyPassword(hash, Password));
        var simulateTimes = Measure(() => hasher.SimulateVerification(Password));

        output.WriteLine($"Argon2id m={PlatformPasswordHasher.MemoryKib}KiB t={PlatformPasswordHasher.Iterations} p={PlatformPasswordHasher.Parallelism}, salt {PlatformPasswordHasher.SaltSize}B, hash {PlatformPasswordHasher.HashSize}B");
        output.WriteLine($"hash     median {Median(hashTimes):F0} ms (min {hashTimes.Min():F0}, max {hashTimes.Max():F0})");
        output.WriteLine($"verify   median {Median(verifyTimes):F0} ms (min {verifyTimes.Min():F0}, max {verifyTimes.Max():F0})");
        output.WriteLine($"simulate median {Median(simulateTimes):F0} ms (min {simulateTimes.Min():F0}, max {simulateTimes.Max():F0})");
        Assert.InRange(Median(verifyTimes), 1, 5_000);
        Assert.InRange(Median(simulateTimes) / Median(verifyTimes), 0.5, 2.0);
    }

    [Theory]
    [InlineData("ops@example.com", "ops@example.com")]
    [InlineData("  Ops.Team+Alerts@Example.COM ", "ops.team+alerts@example.com")]
    [InlineData("a@b.co", "a@b.co")]
    public void EmailNormalizer_AcceptsAsciiAndCanonicalizes(string input, string expected)
    {
        Assert.Equal(expected, PlatformEmailNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("@example.com")]
    [InlineData("ops@localhost")]
    [InlineData("ops@-example.com")]
    [InlineData(".ops@example.com")]
    [InlineData("o..ps@example.com")]
    [InlineData("ops name@example.com")]
    [InlineData("josé@example.com")]
    [InlineData("ops@exämple.com")]
    [InlineData("ops@xn--exmple-cua.com​")]
    [InlineData("ＯＰＳ@example.com")]
    public void EmailNormalizer_RejectsInvalidOrNonAsciiDeterministically(string? input)
    {
        Assert.Null(PlatformEmailNormalizer.TryNormalize(input));
    }

    [Fact]
    public void JwtConfiguration_AcceptsSeparateStrongSettings()
    {
        JwtConfigurationValidator.Validate(TenantOptions(), PlatformOptions(), isProduction: true);
    }

    [Theory]
    [InlineData("platform-missing")]
    [InlineData("platform-short")]
    [InlineData("shared-key")]
    [InlineData("shared-issuer")]
    [InlineData("shared-audience")]
    [InlineData("lifetime-too-long")]
    [InlineData("production-dev-key")]
    [InlineData("tenant-missing")]
    public void JwtConfiguration_FailsClosedOnMissingOrUnsafeSettings(string variant)
    {
        var tenant = TenantOptions();
        var platform = PlatformOptions();
        var isProduction = false;
        switch (variant)
        {
            case "platform-missing": platform = PlatformOptions(signingKey: ""); break;
            case "platform-short": platform = PlatformOptions(signingKey: "too-short-platform-key"); break;
            case "shared-key": platform = PlatformOptions(signingKey: tenant.SigningKey); break;
            case "shared-issuer": platform = PlatformOptions(issuer: tenant.Issuer); break;
            case "shared-audience": platform = PlatformOptions(audience: tenant.Audience); break;
            case "lifetime-too-long": platform = PlatformOptions(minutes: 60); break;
            case "production-dev-key": platform = PlatformOptions(signingKey: "local-development-platform-signing-key-0123456789"); isProduction = true; break;
            default: tenant = new JwtOptions { SigningKey = "" }; break;
        }

        Assert.Throws<InvalidOperationException>(() => JwtConfigurationValidator.Validate(tenant, platform, isProduction));
    }

    [Fact]
    public void AuditWriter_StagesAllowlistedEventWithoutTenant()
    {
        using var dbContext = CreateDbContext();
        var actorId = Guid.NewGuid();

        new PlatformAuditWriter(dbContext).Stage(new PlatformAuditEntry(
            PlatformAuditActions.LoginFailed,
            PlatformAuditOutcome.Failed,
            actorId,
            "req-1",
            new Dictionary<string, string> { ["reason"] = "invalid_credentials", ["failed_login_count"] = "2" }));

        var entry = Assert.Single(dbContext.ChangeTracker.Entries<PlatformAuditLogEntity>()).Entity;
        Assert.Null(entry.TargetTenantId);
        Assert.Equal(actorId, entry.ActorPlatformId);
        Assert.Equal("""{"failed_login_count":"2","reason":"invalid_credentials"}""", entry.MetadataJson);
    }

    [Theory]
    [InlineData("platform.auth.custom", "reason", "x")]
    [InlineData(PlatformAuditActions.LoginFailed, "password", "x")]
    [InlineData(PlatformAuditActions.LoginFailed, "email", "ops@example.com")]
    [InlineData(PlatformAuditActions.LoginFailed, "reason", "Correct Horse Battery")]
    [InlineData(PlatformAuditActions.LoginFailed, "reason", "Bearer eyJhbGciOiJIUzI1NiJ9.e30.sig")]
    [InlineData(PlatformAuditActions.LoginFailed, "reason", "$argon2id$v=19$m=65536")]
    public void AuditWriter_RejectsNonAllowlistedActionKeysOrValues(string action, string key, string value)
    {
        using var dbContext = CreateDbContext();

        Assert.Throws<ArgumentException>(() => new PlatformAuditWriter(dbContext).Stage(new PlatformAuditEntry(
            action,
            PlatformAuditOutcome.Failed,
            null,
            "req-1",
            new Dictionary<string, string> { [key] = value })));
        Assert.Empty(dbContext.ChangeTracker.Entries<PlatformAuditLogEntity>());
    }

    private static List<double> Measure(Action action)
    {
        var times = new List<double>();
        for (var index = 0; index < 5; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return times;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }

    private static JwtOptions TenantOptions() => new() { SigningKey = "tenant-production-signing-key-0123456789-abcdef" };

    private static PlatformJwtOptions PlatformOptions(
        string signingKey = "platform-production-signing-key-9876543210-zyxwvu",
        string issuer = "InternalKnowledgeCopilot.Platform",
        string audience = "InternalKnowledgeCopilot.PlatformAdmin",
        int minutes = 15)
    {
        return new PlatformJwtOptions { SigningKey = signingKey, Issuer = issuer, Audience = audience, AccessTokenMinutes = minutes };
    }

    private static AppDbContext CreateDbContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=:memory:").Options);
    }
}
