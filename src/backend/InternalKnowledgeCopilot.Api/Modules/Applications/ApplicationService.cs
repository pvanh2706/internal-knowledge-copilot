using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Applications;

public interface IApplicationService
{
    Task<IReadOnlyList<ApplicationResponse>> GetApplicationsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    Task<ApplicationResponse> GetApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<ApplicationResponse> CreateApplicationAsync(Guid? actorUserId, CreateApplicationRequest request, CancellationToken cancellationToken = default);

    Task<ApplicationResponse> UpdateApplicationAsync(Guid applicationId, Guid? actorUserId, UpdateApplicationRequest request, CancellationToken cancellationToken = default);

    Task DeleteApplicationAsync(Guid applicationId, Guid? actorUserId, CancellationToken cancellationToken = default);
}

public sealed partial class ApplicationService(AppDbContext dbContext, IAuditLogService auditLogService) : IApplicationService
{
    private const int CodeMaxLength = 100;
    private const int NameMaxLength = 200;
    private const int BaseUrlMaxLength = 1000;

    public async Task<IReadOnlyList<ApplicationResponse>> GetApplicationsAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Applications
            .AsNoTracking()
            .Include(application => application.Tenant)
            .Where(application =>
                application.DeletedAt == null &&
                application.Tenant != null &&
                application.Tenant.DeletedAt == null);

        if (tenantId is not null)
        {
            query = query.Where(application => application.TenantId == tenantId);
        }

        return await query
            .OrderBy(application => application.Tenant!.Code)
            .ThenBy(application => application.Code)
            .Select(application => ToResponse(application))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationResponse> GetApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await dbContext.Applications
            .AsNoTracking()
            .Include(item => item.Tenant)
            .FirstOrDefaultAsync(item =>
                item.Id == applicationId &&
                item.DeletedAt == null &&
                item.Tenant != null &&
                item.Tenant.DeletedAt == null,
                cancellationToken);

        return application is null
            ? throw new KeyNotFoundException("application_not_found")
            : ToResponse(application);
    }

    public async Task<ApplicationResponse> CreateApplicationAsync(
        Guid? actorUserId,
        CreateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(item => item.Id == request.TenantId && item.DeletedAt == null, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException("tenant_not_found");
        }

        var code = NormalizeCode(request.Code, "application_code_required");
        var name = CleanRequired(request.Name, "application_name_required", NameMaxLength);
        var exists = await dbContext.Applications
            .AnyAsync(application => application.TenantId == request.TenantId && application.Code == code, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("application_code_exists");
        }

        var now = DateTimeOffset.UtcNow;
        var application = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Tenant = tenant,
            Code = code,
            Name = name,
            ApplicationType = request.ApplicationType,
            BaseUrl = NormalizeBaseUrl(request.BaseUrl),
            Status = request.Status ?? ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "ApplicationCreated",
            "Application",
            application.Id,
            new
            {
                TenantCode = tenant.Code,
                ApplicationCode = application.Code,
                application.Name,
                application.ApplicationType,
                application.Status
            },
            cancellationToken);

        return ToResponse(application);
    }

    public async Task<ApplicationResponse> UpdateApplicationAsync(
        Guid applicationId,
        Guid? actorUserId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var application = await dbContext.Applications
            .Include(item => item.Tenant)
            .FirstOrDefaultAsync(item =>
                item.Id == applicationId &&
                item.DeletedAt == null &&
                item.Tenant != null &&
                item.Tenant.DeletedAt == null,
                cancellationToken);
        if (application is null)
        {
            throw new KeyNotFoundException("application_not_found");
        }

        if (request.Name is not null)
        {
            application.Name = CleanRequired(request.Name, "application_name_required", NameMaxLength);
        }

        if (request.ApplicationType is not null)
        {
            application.ApplicationType = request.ApplicationType.Value;
        }

        if (request.BaseUrl is not null)
        {
            application.BaseUrl = NormalizeBaseUrl(request.BaseUrl);
        }

        if (request.Status is not null)
        {
            application.Status = request.Status.Value;
        }

        application.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "ApplicationUpdated",
            "Application",
            application.Id,
            new
            {
                TenantCode = application.Tenant!.Code,
                ApplicationCode = application.Code,
                application.Name,
                application.ApplicationType,
                application.Status
            },
            cancellationToken);

        return ToResponse(application);
    }

    public async Task DeleteApplicationAsync(Guid applicationId, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var application = await dbContext.Applications
            .Include(item => item.Tenant)
            .FirstOrDefaultAsync(item =>
                item.Id == applicationId &&
                item.DeletedAt == null &&
                item.Tenant != null &&
                item.Tenant.DeletedAt == null,
                cancellationToken);
        if (application is null)
        {
            throw new KeyNotFoundException("application_not_found");
        }

        var now = DateTimeOffset.UtcNow;
        application.DeletedAt = now;
        application.UpdatedAt = now;
        application.Status = ApplicationStatus.Archived;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "ApplicationDeleted",
            "Application",
            application.Id,
            new
            {
                TenantCode = application.Tenant!.Code,
                ApplicationCode = application.Code,
                application.Name
            },
            cancellationToken);
    }

    private static ApplicationResponse ToResponse(ApplicationEntity application)
    {
        return new ApplicationResponse(
            application.Id,
            application.TenantId,
            application.Tenant?.Code ?? string.Empty,
            application.Code,
            application.Name,
            application.ApplicationType,
            application.BaseUrl,
            application.Status,
            application.CreatedAt,
            application.UpdatedAt);
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
            throw new ArgumentException("invalid_application_code");
        }

        return cleaned;
    }

    private static string? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim().TrimEnd('/');
        if (cleaned.Length > BaseUrlMaxLength)
        {
            throw new ArgumentException("application_base_url_too_long");
        }

        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_application_base_url");
        }

        return cleaned;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
    private static partial Regex SafeCodeRegex();
}
