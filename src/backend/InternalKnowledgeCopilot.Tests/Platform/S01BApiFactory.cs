using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace InternalKnowledgeCopilot.Tests.Platform;

// Chạy Program.cs thật (JwtBearer, TenantResolutionMiddleware, rate limiter, migration, seeder) trên file SQLite tạm.
// Chỉ thay: verifier provisioning (giả lập bằng chứng S01-C đáng tin cậy, hoặc giữ bản thật luôn từ chối) và bỏ worker nền.
public sealed class S01BApiFactory : WebApplicationFactory<Program>
{
    public const string TenantSigningKey = "s01b-test-tenant-signing-key-0123456789-abcdefghijklmnop";
    public const string PlatformSigningKey = "s01b-test-platform-signing-key-9876543210-zyxwvutsrqponml";
    public const string TenantIssuer = "InternalKnowledgeCopilot";
    public const string TenantAudience = "InternalKnowledgeCopilot";
    public const string PlatformIssuer = "InternalKnowledgeCopilot.Platform";
    public const string PlatformAudience = "InternalKnowledgeCopilot.PlatformAdmin";
    public const string SeededTenantPassword = "ChangeMe123!";
    public const string PlatformPassword = "Platform-Initial-Password-1";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ikc-s01b-{Guid.NewGuid():N}");

    public FakeProvisioningVerifier Provisioning { get; } = new() { Verified = true };

    public bool UseRealProvisioningVerifier { get; init; }

    public int PlatformAuthRequestsPerMinute { get; init; } = 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:SqlitePath", Path.Combine(directory, "s01b.db"));
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(directory, "keys"));
        builder.UseSetting("Jwt:SigningKey", TenantSigningKey);
        builder.UseSetting("PlatformJwt:SigningKey", PlatformSigningKey);
        builder.UseSetting("RateLimiting:PlatformAuthRequestsPerMinute", PlatformAuthRequestsPerMinute.ToString());
        builder.ConfigureTestServices(services =>
        {
            foreach (var worker in services.Where(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType == typeof(ProcessingJobWorker)).ToList())
            {
                services.Remove(worker);
            }
            if (!UseRealProvisioningVerifier)
            {
                services.RemoveAll<IPlatformProvisioningVerifier>();
                services.AddSingleton<IPlatformProvisioningVerifier>(Provisioning);
            }

            services.AddControllers().AddApplicationPart(typeof(S01BProbeController).Assembly);
        });
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task ExecuteSqlAsync(string sql)
    {
        return WithDbAsync(async db => await db.Database.ExecuteSqlRawAsync(sql));
    }

    // Mô phỏng kết quả S01-C: tạo tài khoản và (nếu chưa có) trạng thái bootstrap Completed + audit.
    public async Task<PlatformAdminEntity> SeedPlatformAdminAsync(
        string email,
        string password = PlatformPassword,
        bool mustChangePassword = false,
        PlatformAdminStatus status = PlatformAdminStatus.Active,
        bool completeBootstrap = true)
    {
        var now = DateTimeOffset.UtcNow;
        var admin = new PlatformAdminEntity
        {
            Id = Guid.NewGuid(),
            EmailNormalized = email,
            DisplayName = "Platform Operator",
            PasswordHash = new PlatformPasswordHasher().HashPassword(password),
            Status = status,
            MustChangePassword = mustChangePassword,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await WithDbAsync(async db =>
        {
            db.PlatformAdmins.Add(admin);
            if (completeBootstrap && !await db.PlatformSecurityState.AnyAsync())
            {
                db.PlatformSecurityState.Add(new PlatformSecurityStateEntity
                {
                    DeploymentId = Guid.NewGuid(),
                    BootstrapStatus = PlatformBootstrapStatus.Completed,
                    BootstrapAdminId = admin.Id,
                    BootstrapCompletedAt = now,
                    StateVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            return await db.SaveChangesAsync();
        });
        return admin;
    }

    public Task<PlatformAdminEntity> GetPlatformAdminAsync(Guid id)
    {
        return WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync(item => item.Id == id));
    }

    public Task<List<PlatformAuditLogEntity>> GetPlatformAuditAsync()
    {
        return WithDbAsync(async db => (await db.PlatformAuditLogs.AsNoTracking().ToListAsync())
            .OrderBy(item => item.OccurredAt)
            .ToList());
    }

    public async Task<HttpResponseMessage> PlatformLoginAsync(string email, string password)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("/api/platform/auth/login", new { email, password });
    }

    public async Task<string> PlatformTokenAsync(string email, string password = PlatformPassword)
    {
        using var response = await PlatformLoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformLoginResponse>())!.AccessToken;
    }

    public async Task<string> TenantTokenAsync(string email = "admin@example.local", string password = SeededTenantPassword)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-Tenant-Code", "default");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantLoginBody>())!.AccessToken;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null, string? tenantHeader = null)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (tenantHeader is not null)
        {
            request.Headers.Add("X-Tenant-Code", tenantHeader);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    public static string CreateToken(string signingKey, string issuer, string audience, IEnumerable<Claim> claims, DateTime? expires = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);
        var token = new JwtSecurityToken(issuer, audience, claims, expiresAt.AddMinutes(-20), expiresAt, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Claim giống hệt token tenant do JwtTokenService tạo; principalType = null để mô phỏng token legacy.
    public static List<Claim> TenantClaims(Guid userId, Guid tenantId, string tenantCode, UserRole role, string? principalType = PrincipalTypes.Tenant)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new("tenant_id", tenantId.ToString()),
            new("tenant_code", tenantCode),
        };
        if (principalType is not null)
        {
            claims.Add(new Claim(PrincipalClaimTypes.PrincipalType, principalType));
        }

        return claims;
    }

    public static List<Claim> PlatformClaims(Guid adminId, int tokenVersion = 0, string session = PlatformSessionTypes.Full, string? principalType = PrincipalTypes.Platform)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new(PrincipalClaimTypes.TokenVersion, tokenVersion.ToString(), ClaimValueTypes.Integer32),
            new(PrincipalClaimTypes.PlatformSession, session),
        };
        if (principalType is not null)
        {
            claims.Add(new Claim(PrincipalClaimTypes.PrincipalType, principalType));
        }

        return claims;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public sealed record TenantLoginBody(string AccessToken, bool MustChangePassword);
}

public sealed class FakeProvisioningVerifier : IPlatformProvisioningVerifier
{
    public volatile bool Verified;

    public Task<bool> IsProvisioningVerifiedAsync(PlatformSecurityStateEntity state, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Verified);
    }
}

// Endpoint chỉ tồn tại trong test assembly để kiểm chứng policy; production chưa có API quản trị platform (S01-D).
[ApiController]
public sealed class S01BProbeController : ControllerBase
{
    [HttpGet("/api/platform/test-probe/platform-admin")]
    [Authorize(Policy = AppAuthorizationPolicies.PlatformAdminOnly)]
    public IActionResult PlatformAdmin() => Ok(new { principal = User.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value });

    [HttpGet("/api/test-probe/tenant-admin")]
    [Authorize(Policy = AppAuthorizationPolicies.TenantAdminOnly)]
    public IActionResult TenantAdmin() => Ok(new { principal = User.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value });
}
