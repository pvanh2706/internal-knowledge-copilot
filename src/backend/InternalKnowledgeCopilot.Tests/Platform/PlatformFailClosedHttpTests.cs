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

    [Fact]
    public async Task MissingBootstrapState_DeniesLogin()
    {
        using var factory = new S01BApiFactory();
        await factory.SeedPlatformAdminAsync("ops@example.com", completeBootstrap: false);

        using var response = await factory.PlatformLoginAsync("ops@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("bootstrap_state_missing", Assert.Single(await factory.GetPlatformAuditAsync()).MetadataJson);
    }

    [Fact]
    public async Task NotStartedBootstrapState_DeniesLogin()
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
        Assert.Contains("bootstrap_not_completed", Assert.Single(await factory.GetPlatformAuditAsync()).MetadataJson);
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

    [Fact]
    public async Task PlatformLogin_IsRateLimited()
    {
        using var factory = new S01BApiFactory { PlatformAuthRequestsPerMinute = 3 };
        await factory.SeedPlatformAdminAsync("ops@example.com");

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await factory.PlatformLoginAsync("unknown@example.com", "Wrong-Password-123");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
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
