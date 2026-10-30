using System.Net;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform;

// Mỗi test dùng factory riêng vì cần trạng thái DB/provisioning khác nhau (state singleton không thể xóa hay đảo ngược).
public sealed class PlatformFailClosedHttpTests
{
    [Fact]
    public async Task ProductionVerifier_DeniesLogin_EvenWithCompletedBootstrapInDatabase()
    {
        using var factory = new S01BApiFactory { UseRealProvisioningVerifier = true };
        await factory.SeedPlatformAdminAsync("ops@example.com");

        using var response = await factory.PlatformLoginAsync("ops@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("platform_auth_unavailable", await response.Content.ReadAsStringAsync());
        var audit = Assert.Single(await factory.GetPlatformAuditAsync());
        Assert.Equal(PlatformAuditActions.LoginDenied, audit.Action);
        Assert.Null(audit.ActorPlatformId);
        Assert.Contains("provisioning_unverified", audit.MetadataJson);
        Assert.Null((await factory.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync())).LastLoginAt);
    }

    // B-R01: trước bootstrap không ghi platform_audit_logs (chỉ log), để không làm mất điều kiện bootstrap lần đầu của S01-C.
    [Fact]
    public async Task MissingBootstrapState_DeniesLoginWithoutPersistingAudit()
    {
        using var factory = new S01BApiFactory();
        await factory.SeedPlatformAdminAsync("ops@example.com", completeBootstrap: false);

        using var response = await factory.PlatformLoginAsync("ops@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await factory.GetPlatformAuditAsync());
        Assert.Contains(factory.Logs.Messages, message => message.Contains("bootstrap_state_missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NotStartedBootstrapState_DeniesLoginWithoutPersistingAudit()
    {
        using var factory = new S01BApiFactory();
        await factory.SeedPlatformAdminAsync("ops@example.com", completeBootstrap: false);
        await factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            db.PlatformSecurityState.Add(new PlatformSecurityStateEntity { DeploymentId = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now });
            return await db.SaveChangesAsync();
        });

        using var response = await factory.PlatformLoginAsync("ops@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await factory.GetPlatformAuditAsync());
        Assert.Contains(factory.Logs.Messages, message => message.Contains("bootstrap_not_completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FreshDatabase_PreBootstrapLoginAndTokenAttempts_KeepBootstrapPrerequisitesConsistent()
    {
        using var factory = new S01BApiFactory { UseRealProvisioningVerifier = true };
        const string attemptedPassword = "Guessing-Password-123";
        const string attemptedEmail = "attacker-probe@example.com";

        using var login = await factory.PlatformLoginAsync(attemptedEmail, attemptedPassword);
        using var tenantAdminLogin = await factory.PlatformLoginAsync("admin@example.local", S01BApiFactory.SeededTenantPassword);
        // Token ký đúng key platform (ví dụ key bị lộ) trước khi có bootstrap.
        var signedToken = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience,
            S01BApiFactory.PlatformClaims(Guid.NewGuid()));
        using var me = await factory.SendAsync(HttpMethod.Get, "/api/platform/auth/me", signedToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, tenantAdminLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        var counts = await factory.WithDbAsync(async db => (
            Admins: await db.PlatformAdmins.CountAsync(),
            States: await db.PlatformSecurityState.CountAsync(),
            Audit: await db.PlatformAuditLogs.CountAsync()));
        Assert.Equal((0, 0, 0), counts);

        var logs = factory.Logs.Messages;
        Assert.Equal(2, logs.Count(message => message.StartsWith("Platform login refused before bootstrap completion: bootstrap_state_missing", StringComparison.Ordinal)));
        Assert.Single(logs, message => message.StartsWith("Platform token rejected before bootstrap completion: bootstrap_state_missing", StringComparison.Ordinal));
        Assert.All(logs, message =>
        {
            Assert.DoesNotContain(attemptedPassword, message, StringComparison.Ordinal);
            Assert.DoesNotContain(attemptedEmail, message, StringComparison.Ordinal);
            Assert.DoesNotContain(S01BApiFactory.SeededTenantPassword, message, StringComparison.Ordinal);
            Assert.DoesNotContain(signedToken, message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ProvisioningRevokedAfterLogin_InvalidatesExistingTokens()
    {
        using var factory = new S01BApiFactory();
        await factory.SeedPlatformAdminAsync("ops@example.com");
        var token = await factory.PlatformTokenAsync("ops@example.com");

        factory.Provisioning.Verified = false;
        using var me = await factory.SendAsync(HttpMethod.Get, "/api/platform/auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Contains("provisioning_unverified", (await factory.GetPlatformAuditAsync()).Last().MetadataJson);
    }

    [Fact]
    public async Task MandatoryAuditFailure_FailsLoginAndPasswordChangeWithoutStateChange()
    {
        using var factory = new S01BApiFactory();
        var admin = await factory.SeedPlatformAdminAsync("ops@example.com");
        var token = await factory.PlatformTokenAsync("ops@example.com");
        var before = await factory.GetPlatformAdminAsync(admin.Id);
        await factory.ExecuteSqlAsync("""
            CREATE TRIGGER "test_block_platform_audit" BEFORE INSERT ON "platform_audit_logs"
            BEGIN SELECT RAISE(ABORT, 'test_audit_unavailable'); END;
            """);

        using var success = await factory.PlatformLoginAsync("ops@example.com", S01BApiFactory.PlatformPassword);
        using var failure = await factory.PlatformLoginAsync("ops@example.com", "Wrong-Password-123");
        using var change = await factory.SendAsync(HttpMethod.Post, "/api/platform/auth/change-password", token,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = "Never-Committed-Password-1" });

        Assert.Equal(HttpStatusCode.InternalServerError, success.StatusCode);
        Assert.DoesNotContain("accessToken", await success.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, change.StatusCode);
        var after = await factory.GetPlatformAdminAsync(admin.Id);
        Assert.Equal(before.LastLoginAt, after.LastLoginAt);
        Assert.Equal(before.FailedLoginCount, after.FailedLoginCount);
        Assert.Equal(before.TokenVersion, after.TokenVersion);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
    }

    // B-R02: giới hạn đăng nhập platform luôn bật, kể cả khi RateLimiting:Enabled=false.
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task PlatformLogin_IsRateLimited_RegardlessOfGlobalRateLimitingSwitch(string globalEnabled)
    {
        using var factory = new S01BApiFactory
        {
            UniqueClientIpPerRequest = false,
            Settings = new Dictionary<string, string>
            {
                ["RateLimiting:Enabled"] = globalEnabled,
                ["RateLimiting:PlatformAuthRequestsPerMinute"] = "3",
            },
        };
        await factory.SeedPlatformAdminAsync("ops@example.com");

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await factory.PlatformLoginAsync("unknown@example.com", "Wrong-Password-123");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    // Policy không thuộc biên bảo mật giữ hành vi cũ: tắt RateLimiting:Enabled thì endpoint tích hợp không bị giới hạn.
    [Theory]
    [InlineData("true", HttpStatusCode.TooManyRequests)]
    [InlineData("false", HttpStatusCode.Unauthorized)]
    public async Task GlobalRateLimitingSwitch_StillControlsNonSecurityPolicies(string globalEnabled, HttpStatusCode secondResponse)
    {
        using var factory = new S01BApiFactory
        {
            UniqueClientIpPerRequest = false,
            Settings = new Dictionary<string, string>
            {
                ["RateLimiting:Enabled"] = globalEnabled,
                ["RateLimiting:IntegrationRequestsPerMinute"] = "1",
            },
        };

        using var first = await factory.SendAsync(HttpMethod.Get, "/api/integrations/connections", null);
        using var second = await factory.SendAsync(HttpMethod.Get, "/api/integrations/connections", null);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(secondResponse, second.StatusCode);
    }

    [Theory]
    [InlineData("PlatformAuth:MaxFailedLoginAttempts", "0")]
    [InlineData("PlatformAuth:MaxFailedLoginAttempts", "1000")]
    [InlineData("PlatformAuth:LockoutMinutes", "0")]
    [InlineData("PlatformAuth:LockoutMinutes", "1")]
    [InlineData("PlatformAuth:LockoutMinutes", "100000")]
    [InlineData("RateLimiting:PlatformAuthRequestsPerMinute", "0")]
    [InlineData("RateLimiting:PlatformAuthRequestsPerMinute", "10000")]
    public void Startup_FailsClosedOnUnsafePlatformAuthConfiguration(string key, string value)
    {
        using var factory = new S01BApiFactory { Settings = new Dictionary<string, string> { [key] = value } };

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(key, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_DoesNotCreatePlatformAdminOrBootstrapState()
    {
        using var factory = new S01BApiFactory();

        var counts = await factory.WithDbAsync(async db => (await db.PlatformAdmins.CountAsync(), await db.PlatformSecurityState.CountAsync(), await db.Users.CountAsync()));

        Assert.Equal(0, counts.Item1);
        Assert.Equal(0, counts.Item2);
        Assert.True(counts.Item3 > 0, "Seeder tenant vẫn chạy cho môi trường không phải Production.");
    }
}
