using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantCodeHeaderName = "X-Tenant-Code";

    public async Task InvokeAsync(HttpContext httpContext, AppDbContext dbContext, ITenantContext tenantContext)
    {
        // S01-B: đường dẫn platform không bao giờ nhận danh tính tenant từ header, claim hay default tenant.
        if (!httpContext.Request.Path.StartsWithSegments("/api") || httpContext.Request.Path.StartsWithSegments("/api/platform"))
        {
            await next(httpContext);
            return;
        }

        var hasTenantHeader = httpContext.Request.Headers.TryGetValue(TenantCodeHeaderName, out var headerValues)
            && !StringValues.IsNullOrEmpty(headerValues);
        var headerTenantCode = hasTenantHeader ? NormalizeCode(headerValues.FirstOrDefault()) : null;
        if (hasTenantHeader && string.IsNullOrWhiteSpace(headerTenantCode))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new ApiError("tenant_code_required", "Tenant code is required."),
                httpContext.RequestAborted);
            return;
        }

        // Principal tenant đã xác thực: TenantJwt đã đối chiếu tenant_id/tenant_code với tenant còn hoạt động trong DB,
        // nên tenant lấy từ claim; header chỉ được phép trùng khớp, không bao giờ thay thế danh tính đã xác thực.
        if (httpContext.User.Identity?.IsAuthenticated == true
            && string.Equals(httpContext.User.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value, PrincipalTypes.Tenant, StringComparison.Ordinal)
            && Guid.TryParse(httpContext.User.FindFirst(TenantClaimTypes.TenantId)?.Value, out var authenticatedTenantId))
        {
            var claimTenantCode = NormalizeCode(httpContext.User.FindFirst(TenantClaimTypes.TenantCode)?.Value)!;
            if (hasTenantHeader && !string.Equals(headerTenantCode, claimTenantCode, StringComparison.Ordinal))
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await httpContext.Response.WriteAsJsonAsync(
                    new ApiError("tenant_mismatch", "Tenant header does not match the authenticated token."),
                    httpContext.RequestAborted);
                return;
            }

            tenantContext.SetTenant(authenticatedTenantId, claimTenantCode);
            await next(httpContext);
            return;
        }

        // Request chưa xác thực (đăng nhập tenant, tích hợp bằng integration key): chọn tenant theo header hoặc default
        // như trước; không có principal nào được suy ra từ lựa chọn này.
        var tenantCode = headerTenantCode ?? TenantDefaults.DefaultTenantCode;
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            await next(httpContext);
            return;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == tenantCode && item.DeletedAt == null, httpContext.RequestAborted);

        if (tenant is null)
        {
            if (hasTenantHeader)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await httpContext.Response.WriteAsJsonAsync(
                    new ApiError("tenant_not_found", "Tenant was not found."),
                    httpContext.RequestAborted);
                return;
            }

            await next(httpContext);
            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            if (hasTenantHeader)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await httpContext.Response.WriteAsJsonAsync(
                    new ApiError("tenant_not_active", "Tenant is not active."),
                    httpContext.RequestAborted);
                return;
            }

            await next(httpContext);
            return;
        }

        tenantContext.SetTenant(tenant.Id, tenant.Code);
        await next(httpContext);
    }

    private static string? NormalizeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
