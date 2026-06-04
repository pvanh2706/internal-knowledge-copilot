using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Tenants;

public interface ITenantService
{
    Task<IReadOnlyList<TenantResponse>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<TenantResponse> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantResponse> CreateTenantAsync(Guid? actorUserId, CreateTenantRequest request, CancellationToken cancellationToken = default);

    Task<TenantResponse> UpdateTenantAsync(Guid tenantId, Guid? actorUserId, UpdateTenantRequest request, CancellationToken cancellationToken = default);

    Task DeleteTenantAsync(Guid tenantId, Guid? actorUserId, CancellationToken cancellationToken = default);
}

public sealed partial class TenantService(AppDbContext dbContext, IAuditLogService auditLogService) : ITenantService
{
    private const int CodeMaxLength = 100;
    private const int NameMaxLength = 200;

    public async Task<IReadOnlyList<TenantResponse>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.DeletedAt == null)
            .OrderBy(tenant => tenant.Code)
            .Select(tenant => ToResponse(tenant))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantResponse> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId && item.DeletedAt == null, cancellationToken);

        return tenant is null
            ? throw new KeyNotFoundException("tenant_not_found")
            : ToResponse(tenant);
    }

    public async Task<TenantResponse> CreateTenantAsync(
        Guid? actorUserId,
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code, "tenant_code_required");
        var name = CleanRequired(request.Name, "tenant_name_required", NameMaxLength);
        var exists = await dbContext.Tenants.AnyAsync(tenant => tenant.Code == code, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("tenant_code_exists");
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Status = request.Status ?? TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "TenantCreated",
            "Tenant",
            tenant.Id,
            new { tenant.Code, tenant.Name, tenant.Status },
            cancellationToken);

        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateTenantAsync(
        Guid tenantId,
        Guid? actorUserId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(item => item.Id == tenantId && item.DeletedAt == null, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException("tenant_not_found");
        }

        if (request.Name is not null)
        {
            tenant.Name = CleanRequired(request.Name, "tenant_name_required", NameMaxLength);
        }

        if (request.Status is not null)
        {
            tenant.Status = request.Status.Value;
        }

        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "TenantUpdated",
            "Tenant",
            tenant.Id,
            new { tenant.Code, tenant.Name, tenant.Status },
            cancellationToken);

        return ToResponse(tenant);
    }

    public async Task DeleteTenantAsync(Guid tenantId, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(item => item.Id == tenantId && item.DeletedAt == null, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException("tenant_not_found");
        }

        var hasApplications = await dbContext.Applications
            .AnyAsync(application => application.TenantId == tenantId && application.DeletedAt == null, cancellationToken);
        if (hasApplications)
        {
            throw new InvalidOperationException("tenant_has_applications");
        }

        var now = DateTimeOffset.UtcNow;
        tenant.DeletedAt = now;
        tenant.UpdatedAt = now;
        tenant.Status = TenantStatus.Archived;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "TenantDeleted",
            "Tenant",
            tenant.Id,
            new { tenant.Code, tenant.Name },
            cancellationToken);
    }

    private static TenantResponse ToResponse(TenantEntity tenant)
    {
        return new TenantResponse(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            tenant.Status,
            tenant.CreatedAt,
            tenant.UpdatedAt);
    }

    private static string CleanRequired(string value, string errorCode, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorCode);
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException($"{errorCode}_too_long");
        }

        return cleaned;
    }

    private static string NormalizeCode(string value, string errorCode)
    {
        var cleaned = CleanRequired(value, errorCode, CodeMaxLength).ToLowerInvariant();
        if (!SafeCodeRegex().IsMatch(cleaned))
        {
            throw new ArgumentException("invalid_tenant_code");
        }

        return cleaned;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
    private static partial Regex SafeCodeRegex();
}
