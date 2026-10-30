using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Authentication;

// Chạy sau khi TenantJwt đã kiểm tra chữ ký/issuer/audience/hạn. Mọi endpoint tenant (kể cả [Authorize(Roles=...)])
// đi qua bước này nên kiểm tra được áp dụng tập trung:
// - principal_type phải là "tenant"; token legacy không có claim này hoặc mang claim platform bị từ chối;
// - tenant_id/tenant_code phải khớp bản ghi tenant còn hoạt động, user phải còn tồn tại và active trong tenant đó;
// - role lấy từ DB hiện tại thay vì tin role cũ trong JWT;
// - lỗi DB thì fail closed.
public static class TenantPrincipalValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var identity = principal?.Identity as ClaimsIdentity;
        if (principal is null || identity is null)
        {
            context.Fail("tenant_principal_missing");
            return;
        }

        if (!string.Equals(principal.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value, PrincipalTypes.Tenant, StringComparison.Ordinal)
            || principal.HasClaim(claim => claim.Type is PrincipalClaimTypes.TokenVersion or PrincipalClaimTypes.PlatformSession))
        {
            context.Fail("tenant_principal_type_invalid");
            return;
        }

        var tenantCode = principal.FindFirst(TenantClaimTypes.TenantCode)?.Value?.Trim().ToLowerInvariant();
        if (!Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)
            || !Guid.TryParse(principal.FindFirst(TenantClaimTypes.TenantId)?.Value, out var tenantId)
            || string.IsNullOrWhiteSpace(tenantCode))
        {
            context.Fail("tenant_claims_invalid");
            return;
        }

        try
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .Where(item => item.Id == tenantId)
                .Select(item => new { item.Code, item.Status, item.DeletedAt })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (tenant is null
                || tenant.DeletedAt is not null
                || tenant.Status != TenantStatus.Active
                || !string.Equals(tenant.Code, tenantCode, StringComparison.Ordinal))
            {
                context.Fail("tenant_not_valid");
                return;
            }

            var user = await dbContext.Users
                .AsNoTracking()
                .Where(item => item.Id == userId && item.TenantId == tenantId && item.DeletedAt == null && item.IsActive)
                .Select(item => new { item.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (user is null)
            {
                context.Fail("tenant_user_not_valid");
                return;
            }

            foreach (var roleClaim in identity.FindAll(identity.RoleClaimType).ToList())
            {
                identity.RemoveClaim(roleClaim);
            }

            identity.AddClaim(new Claim(identity.RoleClaimType, user.Role.ToString()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Fail("tenant_principal_validation_failed");
        }
    }
}
