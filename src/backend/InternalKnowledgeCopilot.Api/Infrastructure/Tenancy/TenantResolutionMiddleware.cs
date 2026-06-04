using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantCodeHeaderName = "X-Tenant-Code";

    public async Task InvokeAsync(HttpContext httpContext, AppDbContext dbContext, ITenantContext tenantContext)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            await next(httpContext);
            return;
        }

        var hasTenantHeader = httpContext.Request.Headers.TryGetValue(TenantCodeHeaderName, out var headerValues)
            && !StringValues.IsNullOrEmpty(headerValues);
        var rawTenantCode = hasTenantHeader
            ? headerValues.FirstOrDefault()
            : TenantDefaults.DefaultTenantCode;
        var tenantCode = NormalizeCode(rawTenantCode);

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            if (hasTenantHeader)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new ApiError("tenant_code_required", "Tenant code is required."),
                    httpContext.RequestAborted);
                return;
            }

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
