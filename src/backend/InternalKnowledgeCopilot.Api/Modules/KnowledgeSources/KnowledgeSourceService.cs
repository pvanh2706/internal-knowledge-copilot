using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;

public interface IKnowledgeSourceService
{
    Task<IReadOnlyList<KnowledgeSourceResponse>> GetSourcesAsync(Guid? applicationId = null, CancellationToken cancellationToken = default);

    Task<KnowledgeSourceResponse> UpsertSourceAsync(Guid? actorUserId, UpsertKnowledgeSourceRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalObjectResponse>> GetExternalObjectsAsync(Guid? applicationId = null, Guid? knowledgeSourceId = null, CancellationToken cancellationToken = default);

    Task<ExternalObjectResponse> UpsertExternalObjectAsync(Guid? actorUserId, UpsertExternalObjectRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalAclSnapshotResponse>> ReplaceAclSnapshotsAsync(Guid? actorUserId, ReplaceExternalAclSnapshotsRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeSourceEntity> GetOrCreateDefaultLocalSourceAsync(CancellationToken cancellationToken = default);
}

public sealed class KnowledgeSourceService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : IKnowledgeSourceService
{
    private const int NameMaxLength = 300;
    private const int KeyMaxLength = 300;
    private const int ObjectTypeMaxLength = 100;
    private const int TitleMaxLength = 500;
    private const int UrlMaxLength = 1000;
    private const int HashMaxLength = 200;
    private const int StatusTextMaxLength = 100;
    private const int ErrorMaxLength = 2000;

    public async Task<IReadOnlyList<KnowledgeSourceResponse>> GetSourcesAsync(Guid? applicationId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var query = dbContext.KnowledgeSources
            .AsNoTracking()
            .Include(source => source.Application)
            .Where(source =>
                source.TenantId == tenantId &&
                source.DeletedAt == null &&
                source.Application != null &&
                source.Application.DeletedAt == null);

        if (applicationId is not null)
        {
            query = query.Where(source => source.ApplicationId == applicationId);
        }

        return await query
            .OrderBy(source => source.Application!.Code)
            .ThenBy(source => source.SourceType)
            .ThenBy(source => source.ExternalSourceId)
            .Select(source => ToResponse(source))
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSourceResponse> UpsertSourceAsync(
        Guid? actorUserId,
        UpsertKnowledgeSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetApplicationAsync(tenantId, request.ApplicationId, cancellationToken);
        var externalSourceId = NormalizeExternalSourceId(request.SourceType, request.ExternalSourceId);
        var now = DateTimeOffset.UtcNow;
        var source = await dbContext.KnowledgeSources
            .Include(item => item.Application)
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == application.Id &&
                    item.SourceType == request.SourceType &&
                    item.ExternalSourceId == externalSourceId,
                cancellationToken);

        var created = false;
        if (source is null)
        {
            source = new KnowledgeSourceEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ApplicationId = application.Id,
                Application = application,
                SourceType = request.SourceType,
                ExternalSourceId = externalSourceId,
                Name = CleanRequired(request.Name, "knowledge_source_name_required", NameMaxLength),
                SyncMode = request.SyncMode,
                Status = request.Status ?? KnowledgeSourceStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.KnowledgeSources.Add(source);
            created = true;
        }

        source.Name = CleanRequired(request.Name, "knowledge_source_name_required", NameMaxLength);
        source.SyncMode = request.SyncMode;
        source.Status = request.Status ?? source.Status;
        source.MetadataJson = NormalizeJson(request.MetadataJson, "invalid_knowledge_source_metadata");
        source.LastSyncStartedAt = request.LastSyncStartedAt ?? source.LastSyncStartedAt;
        source.LastSyncCompletedAt = request.LastSyncCompletedAt ?? source.LastSyncCompletedAt;
        source.LastSyncStatus = CleanOptional(request.LastSyncStatus, StatusTextMaxLength);
        source.LastSyncError = CleanOptional(request.LastSyncError, ErrorMaxLength);
        source.DeletedAt = null;
        source.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            created ? "KnowledgeSourceCreated" : "KnowledgeSourceUpdated",
            "KnowledgeSource",
            source.Id,
            new { source.ApplicationId, source.SourceType, source.ExternalSourceId, source.Status },
            cancellationToken);

        return ToResponse(source);
    }

    public async Task<IReadOnlyList<ExternalObjectResponse>> GetExternalObjectsAsync(
        Guid? applicationId = null,
        Guid? knowledgeSourceId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var query = dbContext.ExternalObjects
            .AsNoTracking()
            .Include(externalObject => externalObject.Application)
            .Where(externalObject =>
                externalObject.TenantId == tenantId &&
                externalObject.DeletedAt == null &&
                externalObject.Application != null &&
                externalObject.Application.DeletedAt == null);

        if (applicationId is not null)
        {
            query = query.Where(externalObject => externalObject.ApplicationId == applicationId);
        }

        if (knowledgeSourceId is not null)
        {
            query = query.Where(externalObject => externalObject.KnowledgeSourceId == knowledgeSourceId);
        }

        return await query
            .OrderBy(externalObject => externalObject.ObjectType)
            .ThenBy(externalObject => externalObject.ExternalObjectId)
            .Select(externalObject => ToResponse(externalObject))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExternalObjectResponse> UpsertExternalObjectAsync(
        Guid? actorUserId,
        UpsertExternalObjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetApplicationAsync(tenantId, request.ApplicationId, cancellationToken);
        var knowledgeSource = await GetKnowledgeSourceOrNullAsync(tenantId, application.Id, request.KnowledgeSourceId, cancellationToken);
        var objectType = NormalizeKey(request.ObjectType, "object_type_required", ObjectTypeMaxLength, lowerInvariant: true);
        var externalObjectId = NormalizeKey(request.ExternalObjectId, "external_object_id_required", KeyMaxLength, lowerInvariant: false);
        var now = DateTimeOffset.UtcNow;
        var externalObject = await dbContext.ExternalObjects
            .Include(item => item.Application)
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == application.Id &&
                    item.ObjectType == objectType &&
                    item.ExternalObjectId == externalObjectId,
                cancellationToken);

        var created = false;
        if (externalObject is null)
        {
            externalObject = new ExternalObjectEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ApplicationId = application.Id,
                Application = application,
                ObjectType = objectType,
                ExternalObjectId = externalObjectId,
                Title = CleanRequired(request.Title, "external_object_title_required", TitleMaxLength),
                Status = request.Status ?? ExternalObjectStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.ExternalObjects.Add(externalObject);
            created = true;
        }

        externalObject.KnowledgeSourceId = knowledgeSource?.Id;
        externalObject.Title = CleanRequired(request.Title, "external_object_title_required", TitleMaxLength);
        externalObject.Url = NormalizeUrl(request.Url);
        externalObject.MetadataJson = NormalizeJson(request.MetadataJson, "invalid_external_object_metadata");
        externalObject.ContentHash = CleanOptional(request.ContentHash, HashMaxLength);
        externalObject.AclHash = CleanOptional(request.AclHash, HashMaxLength);
        externalObject.Status = request.Status ?? externalObject.Status;
        externalObject.LastSyncedAt = request.LastSyncedAt ?? externalObject.LastSyncedAt ?? now;
        externalObject.ContentSyncedAt = request.ContentSyncedAt ?? externalObject.ContentSyncedAt;
        externalObject.AclSyncedAt = request.AclSyncedAt ?? externalObject.AclSyncedAt;
        externalObject.DeletedAt = null;
        externalObject.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            created ? "ExternalObjectCreated" : "ExternalObjectUpdated",
            "ExternalObject",
            externalObject.Id,
            new { externalObject.ApplicationId, externalObject.ObjectType, externalObject.ExternalObjectId, externalObject.Status },
            cancellationToken);

        return ToResponse(externalObject);
    }

    public async Task<IReadOnlyList<ExternalAclSnapshotResponse>> ReplaceAclSnapshotsAsync(
        Guid? actorUserId,
        ReplaceExternalAclSnapshotsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetApplicationAsync(tenantId, request.ApplicationId, cancellationToken);
        var objectType = NormalizeKey(request.ObjectType, "object_type_required", ObjectTypeMaxLength, lowerInvariant: true);
        var externalObjectId = NormalizeKey(request.ExternalObjectId, "external_object_id_required", KeyMaxLength, lowerInvariant: false);
        var externalObject = await dbContext.ExternalObjects
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == application.Id &&
                    item.ObjectType == objectType &&
                    item.ExternalObjectId == externalObjectId &&
                    item.DeletedAt == null,
                cancellationToken);
        if (externalObject is null)
        {
            throw new KeyNotFoundException("external_object_not_found");
        }

        var syncedAt = DateTimeOffset.UtcNow;
        var requestedAclSnapshots = request.AclSnapshots ?? throw new ArgumentException("acl_snapshots_required");
        var snapshots = requestedAclSnapshots
            .Select(item => ToEntity(tenantId, application.Id, externalObject.Id, objectType, externalObjectId, item, syncedAt))
            .ToList();
        var duplicateSnapshot = snapshots
            .GroupBy(snapshot => new { snapshot.SubjectType, snapshot.SubjectId, snapshot.Permission })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSnapshot is not null)
        {
            throw new ArgumentException("duplicate_acl_snapshot");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.ExternalAclSnapshots
            .Where(snapshot =>
                snapshot.TenantId == tenantId &&
                snapshot.ApplicationId == application.Id &&
                snapshot.ObjectType == objectType &&
                snapshot.ExternalObjectId == externalObjectId)
            .ToListAsync(cancellationToken);
        dbContext.ExternalAclSnapshots.RemoveRange(existing);

        dbContext.ExternalAclSnapshots.AddRange(snapshots);
        externalObject.AclSyncedAt = syncedAt;
        externalObject.LastSyncedAt = syncedAt;
        externalObject.UpdatedAt = syncedAt;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "ExternalAclSnapshotsReplaced",
            "ExternalObject",
            externalObject.Id,
            new { externalObject.ApplicationId, externalObject.ObjectType, externalObject.ExternalObjectId, SnapshotCount = snapshots.Count },
            cancellationToken);

        return snapshots
            .OrderBy(snapshot => snapshot.SubjectType)
            .ThenBy(snapshot => snapshot.SubjectId)
            .ThenBy(snapshot => snapshot.Permission)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<KnowledgeSourceEntity> GetOrCreateDefaultLocalSourceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetOrCreateDefaultApplicationAsync(tenantId, cancellationToken);
        var source = await dbContext.KnowledgeSources.FirstOrDefaultAsync(
            item =>
                item.TenantId == tenantId &&
                item.ApplicationId == application.Id &&
                item.SourceType == KnowledgeSourceKind.Local &&
                item.ExternalSourceId == TenantDefaults.DefaultLocalKnowledgeSourceExternalId,
            cancellationToken);
        if (source is not null)
        {
            return source;
        }

        var now = DateTimeOffset.UtcNow;
        source = new KnowledgeSourceEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = application.Id,
            SourceType = KnowledgeSourceKind.Local,
            ExternalSourceId = TenantDefaults.DefaultLocalKnowledgeSourceExternalId,
            Name = "Local uploads and wiki",
            SyncMode = KnowledgeSourceSyncMode.Manual,
            Status = KnowledgeSourceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.KnowledgeSources.Add(source);
        await dbContext.SaveChangesAsync(cancellationToken);
        return source;
    }

    private async Task<ApplicationEntity> GetApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications.FirstOrDefaultAsync(
            item =>
                item.TenantId == tenantId &&
                item.Id == applicationId &&
                item.DeletedAt == null &&
                item.Status == ApplicationStatus.Active,
            cancellationToken);

        return application ?? throw new KeyNotFoundException("application_not_found");
    }

    private async Task<ApplicationEntity> GetOrCreateDefaultApplicationAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications.FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.Code == TenantDefaults.DefaultApplicationCode && item.DeletedAt == null,
            cancellationToken);
        if (application is not null)
        {
            return application;
        }

        var now = DateTimeOffset.UtcNow;
        application = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = TenantDefaults.DefaultApplicationCode,
            Name = "Internal Knowledge Copilot",
            ApplicationType = ApplicationType.Internal,
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        return application;
    }

    private async Task<KnowledgeSourceEntity?> GetKnowledgeSourceOrNullAsync(
        Guid tenantId,
        Guid applicationId,
        Guid? knowledgeSourceId,
        CancellationToken cancellationToken)
    {
        if (knowledgeSourceId is null)
        {
            return null;
        }

        var source = await dbContext.KnowledgeSources.FirstOrDefaultAsync(
            item =>
                item.TenantId == tenantId &&
                item.ApplicationId == applicationId &&
                item.Id == knowledgeSourceId &&
                item.DeletedAt == null,
            cancellationToken);

        return source ?? throw new KeyNotFoundException("knowledge_source_not_found");
    }

    private static ExternalAclSnapshotEntity ToEntity(
        Guid tenantId,
        Guid applicationId,
        Guid externalObjectRecordId,
        string objectType,
        string externalObjectId,
        ExternalAclSnapshotItemRequest request,
        DateTimeOffset syncedAt)
    {
        return new ExternalAclSnapshotEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            ExternalObjectRecordId = externalObjectRecordId,
            ObjectType = objectType,
            ExternalObjectId = externalObjectId,
            SubjectType = NormalizeKey(request.SubjectType, "subject_type_required", ObjectTypeMaxLength, lowerInvariant: true),
            SubjectId = NormalizeKey(request.SubjectId, "subject_id_required", KeyMaxLength, lowerInvariant: false),
            SubjectDisplayName = CleanOptional(request.SubjectDisplayName, NameMaxLength),
            Permission = request.Permission,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            MetadataJson = NormalizeJson(request.MetadataJson, "invalid_acl_metadata"),
            SyncedAt = syncedAt,
        };
    }

    private static KnowledgeSourceResponse ToResponse(KnowledgeSourceEntity source)
    {
        return new KnowledgeSourceResponse(
            source.Id,
            source.TenantId,
            source.ApplicationId,
            source.Application?.Code ?? string.Empty,
            source.SourceType,
            source.ExternalSourceId,
            source.Name,
            source.SyncMode,
            source.Status,
            source.MetadataJson,
            source.LastSyncStartedAt,
            source.LastSyncCompletedAt,
            source.LastSyncStatus,
            source.LastSyncError,
            source.CreatedAt,
            source.UpdatedAt);
    }

    private static ExternalObjectResponse ToResponse(ExternalObjectEntity externalObject)
    {
        return new ExternalObjectResponse(
            externalObject.Id,
            externalObject.TenantId,
            externalObject.ApplicationId,
            externalObject.Application?.Code ?? string.Empty,
            externalObject.KnowledgeSourceId,
            externalObject.ObjectType,
            externalObject.ExternalObjectId,
            externalObject.Title,
            externalObject.Url,
            externalObject.MetadataJson,
            externalObject.ContentHash,
            externalObject.AclHash,
            externalObject.Status,
            externalObject.LastSyncedAt,
            externalObject.ContentSyncedAt,
            externalObject.AclSyncedAt,
            externalObject.CreatedAt,
            externalObject.UpdatedAt);
    }

    private static ExternalAclSnapshotResponse ToResponse(ExternalAclSnapshotEntity snapshot)
    {
        return new ExternalAclSnapshotResponse(
            snapshot.Id,
            snapshot.ExternalObjectRecordId,
            snapshot.ObjectType,
            snapshot.ExternalObjectId,
            snapshot.SubjectType,
            snapshot.SubjectId,
            snapshot.SubjectDisplayName,
            snapshot.Permission,
            snapshot.ValidFrom,
            snapshot.ValidTo,
            snapshot.MetadataJson,
            snapshot.SyncedAt);
    }

    private static string NormalizeExternalSourceId(KnowledgeSourceKind sourceType, string? value)
    {
        return sourceType == KnowledgeSourceKind.Local
            ? TenantDefaults.DefaultLocalKnowledgeSourceExternalId
            : NormalizeKey(value, "external_source_id_required", KeyMaxLength, lowerInvariant: false);
    }

    private static string NormalizeKey(string? value, string errorCode, int maxLength, bool lowerInvariant)
    {
        var cleaned = CleanRequired(value ?? string.Empty, errorCode, maxLength);
        return lowerInvariant ? cleaned.ToLowerInvariant() : cleaned;
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

    private static string? CleanOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException("value_too_long");
        }

        return cleaned;
    }

    private static string? NormalizeUrl(string? value)
    {
        var cleaned = CleanOptional(value, UrlMaxLength);
        if (cleaned is null)
        {
            return null;
        }

        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_external_object_url");
        }

        return cleaned;
    }

    private static string? NormalizeJson(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            throw new ArgumentException(errorCode);
        }
    }
}
