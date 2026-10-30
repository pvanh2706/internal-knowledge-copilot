using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform;

// B1/B4: TenantJwt và PlatformJwt độc lập, chạy qua pipeline xác thực thật của Program.cs.
public sealed class DualSchemeIsolationHttpTests(S01BApiFactory factory) : IClassFixture<S01BApiFactory>
{
    private const string PlatformMe = "/api/platform/auth/me";
    private const string PlatformProbe = "/api/platform/test-probe/platform-admin";
    private const string TenantUsers = "/api/users";

    [Fact]
    public async Task TenantLogin_StillWorks_AndIssuesTenantPrincipal()
    {
        var token = await factory.TenantTokenAsync();

        using var users = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);
        using var tenantAdmin = await factory.SendAsync(HttpMethod.Get, "/api/test-probe/tenant-admin", token);

        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tenantAdmin.StatusCode);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(PrincipalTypes.Tenant, jwt.Claims.Single(claim => claim.Type == PrincipalClaimTypes.PrincipalType).Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type is PrincipalClaimTypes.TokenVersion or PrincipalClaimTypes.PlatformSession);
    }

    [Fact]
    public async Task TenantToken_IsRejectedByPlatformEndpoints()
    {
        var token = await factory.TenantTokenAsync();

        using var me = await factory.SendAsync(HttpMethod.Get, PlatformMe, token);
        using var probe = await factory.SendAsync(HttpMethod.Get, PlatformProbe, token);
        using var change = await factory.SendAsync(HttpMethod.Post, "/api/platform/auth/change-password", token, new { currentPassword = "x", newPassword = "y" });

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, probe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
    }

    [Fact]
    public async Task PlatformToken_IsRejectedByTenantEndpoints()
    {
        await factory.SeedPlatformAdminAsync("isolation@example.com");
        var token = await factory.PlatformTokenAsync("isolation@example.com");

        using var users = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);
        using var tenantAdmin = await factory.SendAsync(HttpMethod.Get, "/api/test-probe/tenant-admin", token);
        using var tenantChange = await factory.SendAsync(HttpMethod.Post, "/api/auth/change-password", token, new { currentPassword = "x", newPassword = "y" });
        using var platformProbe = await factory.SendAsync(HttpMethod.Get, PlatformProbe, token);

        Assert.Equal(HttpStatusCode.Unauthorized, users.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tenantAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tenantChange.StatusCode);
        Assert.Equal(HttpStatusCode.OK, platformProbe.StatusCode);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("platform-key")]
    [InlineData("expired")]
    public async Task TenantToken_WithWrongKeyIssuerAudienceOrExpiry_IsRejected(string variant)
    {
        var (userId, tenantId) = await GetSeededAdminAsync();
        var claims = S01BApiFactory.TenantClaims(userId, tenantId, "default", UserRole.Admin);
        var token = variant switch
        {
            "key" => S01BApiFactory.CreateToken("another-tenant-signing-key-0123456789-abcdefghijkl", S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience, claims),
            "issuer" => S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, "attacker", S01BApiFactory.TenantAudience, claims),
            "audience" => S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, "attacker", claims),
            "platform-key" => S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience, claims),
            _ => S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience, claims, DateTime.UtcNow.AddMinutes(-10)),
        };
        var control = S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience, claims);

        using var rejected = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);
        using var accepted = await factory.SendAsync(HttpMethod.Get, TenantUsers, control);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("tenant-key")]
    public async Task PlatformToken_WithWrongKeyIssuerOrAudience_IsRejected(string variant)
    {
        var admin = await factory.SeedPlatformAdminAsync($"wrong-{variant}@example.com");
        var claims = S01BApiFactory.PlatformClaims(admin.Id);
        var token = variant switch
        {
            "key" => S01BApiFactory.CreateToken("another-platform-signing-key-0123456789-abcdefgh", S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience, claims),
            "issuer" => S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.PlatformAudience, claims),
            "audience" => S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.TenantAudience, claims),
            _ => S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience, claims),
        };
        var control = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience, claims);

        using var rejected = await factory.SendAsync(HttpMethod.Get, PlatformProbe, token);
        using var accepted = await factory.SendAsync(HttpMethod.Get, PlatformProbe, control);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task LegacyOrForgedPrincipalTypes_AreRejected()
    {
        var (userId, tenantId) = await GetSeededAdminAsync();
        var admin = await factory.SeedPlatformAdminAsync("forged@example.com");

        // Token tenant định dạng cũ (không có principal_type), đúng key/issuer/audience.
        var legacyTenant = S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience,
            S01BApiFactory.TenantClaims(userId, tenantId, "default", UserRole.Admin, principalType: null));
        // Token ký bằng key tenant nhưng tự khai principal_type=platform.
        var forgedTenant = S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience,
            S01BApiFactory.TenantClaims(userId, tenantId, "default", UserRole.Admin, principalType: PrincipalTypes.Platform));
        // Token ký bằng key platform nhưng thiếu/khai sai principal_type, hoặc mang danh tính tenant.
        var platformWithoutType = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience,
            S01BApiFactory.PlatformClaims(admin.Id, principalType: null));
        var platformClaimingTenant = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience,
            S01BApiFactory.PlatformClaims(admin.Id, principalType: PrincipalTypes.Tenant));
        var platformWithTenantIdentity = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience,
            [.. S01BApiFactory.PlatformClaims(admin.Id), new("tenant_id", tenantId.ToString()), new("tenant_code", "default")]);

        foreach (var token in new[] { legacyTenant, forgedTenant })
        {
            using var tenantResponse = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);
            using var platformResponse = await factory.SendAsync(HttpMethod.Get, PlatformProbe, token);
            Assert.Equal(HttpStatusCode.Unauthorized, tenantResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, platformResponse.StatusCode);
        }

        foreach (var token in new[] { platformWithoutType, platformClaimingTenant, platformWithTenantIdentity })
        {
            using var platformResponse = await factory.SendAsync(HttpMethod.Get, PlatformProbe, token);
            using var tenantResponse = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);
            Assert.Equal(HttpStatusCode.Unauthorized, platformResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, tenantResponse.StatusCode);
        }
    }

    [Fact]
    public async Task TenantToken_WithMismatchedTenantIdAndCode_IsRejected()
    {
        var (userId, tenantId) = await GetSeededAdminAsync();
        var other = await CreateTenantAsync($"acme-{Guid.NewGuid():N}"[..20], TenantStatus.Active);

        var codeOfOtherTenant = CreateTenantToken(userId, tenantId, other.Code, UserRole.Admin);
        var idOfOtherTenant = CreateTenantToken(userId, other.Id, "default", UserRole.Admin);

        using var first = await factory.SendAsync(HttpMethod.Get, TenantUsers, codeOfOtherTenant);
        using var second = await factory.SendAsync(HttpMethod.Get, TenantUsers, idOfOtherTenant);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task ValidTenantToken_WithDifferentTenantHeader_IsConflict_AndHeaderNeverReplacesIdentity()
    {
        var other = await CreateTenantAsync($"beta-{Guid.NewGuid():N}"[..20], TenantStatus.Active);
        var token = await factory.TenantTokenAsync();

        using var mismatch = await factory.SendAsync(HttpMethod.Get, TenantUsers, token, tenantHeader: other.Code);
        using var match = await factory.SendAsync(HttpMethod.Get, TenantUsers, token, tenantHeader: "default");

        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, match.StatusCode);
    }

    [Theory]
    [InlineData(TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Archived, false)]
    [InlineData(TenantStatus.Active, true)]
    public async Task TenantToken_ForInactiveOrDeletedTenant_IsRejected(TenantStatus status, bool deleted)
    {
        var tenant = await CreateTenantAsync($"t-{Guid.NewGuid():N}"[..20], status, deleted);
        var userId = await CreateTenantUserAsync(tenant.Id, UserRole.Admin, isActive: true);
        var token = CreateTenantToken(userId, tenant.Id, tenant.Code, UserRole.Admin);

        using var response = await factory.SendAsync(HttpMethod.Get, TenantUsers, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantToken_UsesCurrentDatabaseRoleAndUserStatus()
    {
        var tenantId = await factory.WithDbAsync(db => db.Tenants.Where(item => item.Code == "default").Select(item => item.Id).SingleAsync());
        var demotedUserId = await CreateTenantUserAsync(tenantId, UserRole.User, isActive: true);
        var inactiveUserId = await CreateTenantUserAsync(tenantId, UserRole.Admin, isActive: false);

        // JWT còn ghi role Admin nhưng DB hiện tại là User: quyền theo DB.
        using var demoted = await factory.SendAsync(HttpMethod.Get, TenantUsers, CreateTenantToken(demotedUserId, tenantId, "default", UserRole.Admin));
        using var inactive = await factory.SendAsync(HttpMethod.Get, TenantUsers, CreateTenantToken(inactiveUserId, tenantId, "default", UserRole.Admin));

        Assert.Equal(HttpStatusCode.Forbidden, demoted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
    }

    [Fact]
    public async Task PlatformPath_IgnoresTenantHeader()
    {
        await factory.SeedPlatformAdminAsync("header@example.com");
        var token = await factory.PlatformTokenAsync("header@example.com");

        using var unknownTenant = await factory.SendAsync(HttpMethod.Get, PlatformMe, token, tenantHeader: "no-such-tenant");
        using var anonymousLogin = await factory.SendAsync(
            HttpMethod.Post,
            "/api/platform/auth/login",
            null,
            new { email = "admin@example.local", password = S01BApiFactory.SeededTenantPassword },
            tenantHeader: "default");

        Assert.Equal(HttpStatusCode.OK, unknownTenant.StatusCode);
        // Tenant Admin không bao giờ đăng nhập được vào platform.
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLogin.StatusCode);
    }

    [Fact]
    public async Task TenantChangePassword_RegressionStillWorks()
    {
        var tenantId = await factory.WithDbAsync(db => db.Tenants.Where(item => item.Code == "default").Select(item => item.Id).SingleAsync());
        var userId = await CreateTenantUserAsync(tenantId, UserRole.User, isActive: true);
        var email = await factory.WithDbAsync(db => db.Users.Where(item => item.Id == userId).Select(item => item.Email).SingleAsync());
        var token = await factory.TenantTokenAsync(email, "Tenant-Password-1");

        using var response = await factory.SendAsync(HttpMethod.Post, "/api/auth/change-password", token,
            new { currentPassword = "Tenant-Password-1", newPassword = "Tenant-Password-2" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await factory.TenantTokenAsync(email, "Tenant-Password-2")));
    }

    private async Task<(Guid UserId, Guid TenantId)> GetSeededAdminAsync()
    {
        await factory.TenantTokenAsync();
        return await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Email == "admin@example.local");
            return (user.Id, user.TenantId);
        });
    }

    private static string CreateTenantToken(Guid userId, Guid tenantId, string tenantCode, UserRole role)
    {
        return S01BApiFactory.CreateToken(S01BApiFactory.TenantSigningKey, S01BApiFactory.TenantIssuer, S01BApiFactory.TenantAudience,
            S01BApiFactory.TenantClaims(userId, tenantId, tenantCode, role));
    }

    private Task<TenantEntity> CreateTenantAsync(string code, TenantStatus status, bool deleted = false)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity { Id = Guid.NewGuid(), Code = code, Name = code, Status = status, CreatedAt = now, UpdatedAt = now, DeletedAt = deleted ? now : null };
        return factory.WithDbAsync(async db =>
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return tenant;
        });
    }

    private Task<Guid> CreateTenantUserAsync(Guid tenantId, UserRole role, bool isActive)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = $"user-{Guid.NewGuid():N}@example.local",
            DisplayName = "Test User",
            PasswordHash = new InternalKnowledgeCopilot.Api.Modules.Auth.PasswordHasher().HashPassword("Tenant-Password-1"),
            Role = role,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
    }
}
