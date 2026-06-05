using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Integrations;

public interface IIntegrationService
{
    Task<IReadOnlyList<IntegrationConnectionResponse>> GetConnectionsAsync(Guid? applicationId = null, CancellationToken cancellationToken = default);

    Task<IntegrationConnectionResponse> CreateConnectionAsync(Guid? actorUserId, CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default);

    Task<IntegrationInboundEventResponse> ReceiveDomainEventAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        DomainIntegrationEventRequest request,
        CancellationToken cancellationToken = default);

    Task<IntegrationInboundEventResponse> ReceiveDocumentChangedAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        DocumentChangedIntegrationRequest request,
        CancellationToken cancellationToken = default);

    Task<IntegrationInboundEventResponse> ReceiveObjectSyncAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        ObjectSyncIntegrationRequest request,
        CancellationToken cancellationToken = default);

    Task<IntegrationInboundEventResponse> ReceivePermissionSyncAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        PermissionSyncIntegrationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IntegrationService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IProcessingJobService processingJobService,
    IIntegrationSecretHasher secretHasher,
    ILogger<IntegrationService>? logger = null) : IIntegrationService
{
    private const int CodeMaxLength = 100;
    private const int NameMaxLength = 200;
    private const int BaseUrlMaxLength = 1000;
    private const int SecretReferenceMaxLength = 200;
    private const int IdempotencyKeyMaxLength = 200;
    private const int ExternalIdMaxLength = 300;
    private const int ObjectTypeMaxLength = 100;
    private const int EventTypeMaxLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<IntegrationConnectionResponse>> GetConnectionsAsync(Guid? applicationId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var query = dbContext.IntegrationConnections
            .AsNoTracking()
            .Include(connection => connection.Application)
            .Where(connection =>
                connection.TenantId == tenantId &&
                connection.DeletedAt == null &&
                connection.Application != null &&
                connection.Application.DeletedAt == null);

        if (applicationId is not null)
        {
            query = query.Where(connection => connection.ApplicationId == applicationId);
        }

        return await query
            .OrderBy(connection => connection.Application!.Code)
            .ThenBy(connection => connection.Name)
            .Select(connection => ToResponse(connection))
            .ToListAsync(cancellationToken);
    }

    public async Task<IntegrationConnectionResponse> CreateConnectionAsync(
        Guid? actorUserId,
        CreateIntegrationConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetApplicationByIdAsync(tenantId, request.ApplicationId, cancellationToken);
        var secretReference = CleanRequired(request.SecretReference, "integration_secret_reference_required", SecretReferenceMaxLength);
        var exists = await dbContext.IntegrationConnections.AnyAsync(
            connection =>
                connection.TenantId == tenantId &&
                connection.ApplicationId == application.Id &&
                connection.SecretReference == secretReference &&
                connection.DeletedAt == null,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("integration_secret_reference_exists");
        }

        var now = DateTimeOffset.UtcNow;
        var connection = new IntegrationConnectionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = application.Id,
            Application = application,
            Name = CleanRequired(request.Name, "integration_connection_name_required", NameMaxLength),
            BaseUrl = NormalizeBaseUrl(request.BaseUrl),
            AuthMode = request.AuthMode,
            Status = request.Status ?? IntegrationConnectionStatus.Active,
            SecretReference = secretReference,
            SecretHash = request.AuthMode == IntegrationAuthMode.InternalApiKey
                ? secretHasher.HashSecret(request.SecretValue ?? string.Empty)
                : null,
            SecretRotatedAt = request.AuthMode == IntegrationAuthMode.InternalApiKey ? now : null,
            MetadataJson = NormalizeJson(request.MetadataJson, "invalid_integration_connection_metadata"),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.IntegrationConnections.Add(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "IntegrationConnectionCreated",
            "IntegrationConnection",
            connection.Id,
            new { connection.ApplicationId, connection.SecretReference, connection.AuthMode, connection.Status },
            cancellationToken);

        return ToResponse(connection);
    }

    public Task<IntegrationInboundEventResponse> ReceiveDomainEventAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        DomainIntegrationEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var eventType = CleanRequired(request.EventType, "integration_event_type_required", EventTypeMaxLength);
        return ReceiveInboundEventAsync(
            applicationCode,
            authentication,
            IntegrationInboundEventType.DomainEvent,
            request.IdempotencyKey,
            request.ExternalEventId,
            request.ObjectType,
            request.ExternalObjectId,
            ValidateAndSerialize(
                request with
                {
                    EventType = eventType,
                    PayloadJson = NormalizeJson(request.PayloadJson, "invalid_integration_event_payload"),
                    MetadataJson = NormalizeJson(request.MetadataJson, "invalid_integration_event_metadata")
                }),
            cancellationToken);
    }

    public Task<IntegrationInboundEventResponse> ReceiveDocumentChangedAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        DocumentChangedIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var externalDocumentId = CleanRequired(request.ExternalDocumentId, "external_document_id_required", ExternalIdMaxLength);
        return ReceiveInboundEventAsync(
            applicationCode,
            authentication,
            IntegrationInboundEventType.DocumentChanged,
            request.IdempotencyKey,
            externalDocumentId,
            "document",
            externalDocumentId,
            ValidateAndSerialize(
                request with
                {
                    ExternalDocumentId = externalDocumentId,
                    ChangeType = CleanRequired(request.ChangeType, "document_change_type_required", EventTypeMaxLength).ToLowerInvariant(),
                    KnowledgeSourceExternalId = CleanOptional(request.KnowledgeSourceExternalId, ExternalIdMaxLength),
                    Title = CleanOptional(request.Title, NameMaxLength),
                    Url = NormalizeOptionalUrl(request.Url),
                    ContentHash = CleanOptional(request.ContentHash, ExternalIdMaxLength),
                    MetadataJson = NormalizeJson(request.MetadataJson, "invalid_document_changed_metadata")
                }),
            cancellationToken);
    }

    public Task<IntegrationInboundEventResponse> ReceiveObjectSyncAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        ObjectSyncIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var objectType = NormalizeKey(request.ObjectType, "object_type_required", ObjectTypeMaxLength, lowerInvariant: true);
        var externalObjectId = CleanRequired(request.ExternalObjectId, "external_object_id_required", ExternalIdMaxLength);
        return ReceiveInboundEventAsync(
            applicationCode,
            authentication,
            IntegrationInboundEventType.ObjectSync,
            request.IdempotencyKey,
            externalObjectId,
            objectType,
            externalObjectId,
            ValidateAndSerialize(
                request with
                {
                    ObjectType = objectType,
                    ExternalObjectId = externalObjectId,
                    Title = CleanRequired(request.Title, "external_object_title_required", 500),
                    KnowledgeSourceExternalId = CleanOptional(request.KnowledgeSourceExternalId, ExternalIdMaxLength),
                    Url = NormalizeOptionalUrl(request.Url),
                    ContentHash = CleanOptional(request.ContentHash, ExternalIdMaxLength),
                    AclHash = CleanOptional(request.AclHash, ExternalIdMaxLength),
                    MetadataJson = NormalizeJson(request.MetadataJson, "invalid_object_sync_metadata")
                }),
            cancellationToken);
    }

    public Task<IntegrationInboundEventResponse> ReceivePermissionSyncAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        PermissionSyncIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var objectType = NormalizeKey(request.ObjectType, "object_type_required", ObjectTypeMaxLength, lowerInvariant: true);
        var externalObjectId = CleanRequired(request.ExternalObjectId, "external_object_id_required", ExternalIdMaxLength);
        var snapshots = request.AclSnapshots ?? throw new ArgumentException("acl_snapshots_required");
        var normalizedSnapshots = snapshots
            .Select(snapshot => snapshot with
            {
                SubjectType = NormalizeKey(snapshot.SubjectType, "subject_type_required", ObjectTypeMaxLength, lowerInvariant: true),
                SubjectId = CleanRequired(snapshot.SubjectId, "subject_id_required", ExternalIdMaxLength),
                SubjectDisplayName = CleanOptional(snapshot.SubjectDisplayName, NameMaxLength),
                MetadataJson = NormalizeJson(snapshot.MetadataJson, "invalid_acl_snapshot_metadata")
            })
            .ToList();

        var duplicateSnapshot = normalizedSnapshots
            .GroupBy(snapshot => new { snapshot.SubjectType, snapshot.SubjectId, snapshot.Permission })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSnapshot is not null)
        {
            throw new ArgumentException("duplicate_acl_snapshot");
        }

        return ReceiveInboundEventAsync(
            applicationCode,
            authentication,
            IntegrationInboundEventType.PermissionSync,
            request.IdempotencyKey,
            externalObjectId,
            objectType,
            externalObjectId,
            ValidateAndSerialize(
                request with
                {
                    ObjectType = objectType,
                    ExternalObjectId = externalObjectId,
                    AclSnapshots = normalizedSnapshots,
                    MetadataJson = NormalizeJson(request.MetadataJson, "invalid_permission_sync_metadata")
                }),
            cancellationToken);
    }

    private async Task<IntegrationInboundEventResponse> ReceiveInboundEventAsync(
        string applicationCode,
        IntegrationAuthenticationRequest authentication,
        IntegrationInboundEventType eventType,
        string rawIdempotencyKey,
        string? rawExternalEventId,
        string? rawObjectType,
        string? rawExternalObjectId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetApplicationByCodeAsync(tenantId, applicationCode, cancellationToken);
        var connection = await AuthenticateConnectionAsync(tenantId, application.Id, authentication, cancellationToken);
        var idempotencyKey = CleanRequired(rawIdempotencyKey, "idempotency_key_required", IdempotencyKeyMaxLength);
        var existing = await FindExistingInboundEventAsync(tenantId, application.Id, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ToResponse(existing, application.Code, isDuplicate: true);
        }

        var now = DateTimeOffset.UtcNow;
        var inboundEvent = new IntegrationInboundEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = application.Id,
            IntegrationConnectionId = connection.Id,
            EventType = eventType,
            IdempotencyKey = idempotencyKey,
            ExternalEventId = CleanOptional(rawExternalEventId, ExternalIdMaxLength),
            ObjectType = rawObjectType is null ? null : NormalizeKey(rawObjectType, "object_type_required", ObjectTypeMaxLength, lowerInvariant: true),
            ExternalObjectId = CleanOptional(rawExternalObjectId, ExternalIdMaxLength),
            PayloadJson = payloadJson,
            Status = IntegrationInboundEventStatus.Received,
            ReceivedAt = now,
            CreatedAt = now,
        };

        dbContext.IntegrationInboundEvents.Add(inboundEvent);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(inboundEvent).State = EntityState.Detached;
            existing = await FindExistingInboundEventAsync(tenantId, application.Id, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return ToResponse(existing, application.Code, isDuplicate: true);
            }

            throw;
        }

        await auditLogService.RecordAsync(
            null,
            "IntegrationInboundEventReceived",
            "IntegrationInboundEvent",
            inboundEvent.Id,
            new { inboundEvent.ApplicationId, inboundEvent.EventType, inboundEvent.IdempotencyKey, inboundEvent.ObjectType, inboundEvent.ExternalObjectId },
            cancellationToken);
        await EnqueueInboundProcessingJobAsync(inboundEvent, cancellationToken);
        logger?.LogInformation(
            "Integration inbound event {InboundEventId} received for tenant {TenantId} application {ApplicationId} type {EventType} object {ObjectType}/{ExternalObjectId}.",
            inboundEvent.Id,
            inboundEvent.TenantId,
            inboundEvent.ApplicationId,
            inboundEvent.EventType,
            inboundEvent.ObjectType,
            inboundEvent.ExternalObjectId);

        return ToResponse(inboundEvent, application.Code, isDuplicate: false);
    }

    private async Task EnqueueInboundProcessingJobAsync(IntegrationInboundEventEntity inboundEvent, CancellationToken cancellationToken)
    {
        var jobType = inboundEvent.EventType switch
        {
            IntegrationInboundEventType.ObjectSync => ProcessingJobTypes.ObjectSync,
            IntegrationInboundEventType.PermissionSync => ProcessingJobTypes.PermissionSync,
            _ => null
        };
        if (jobType is null)
        {
            return;
        }

        await processingJobService.EnqueueAsync(
            new ProcessingJobEnqueueRequest(
                inboundEvent.TenantId,
                inboundEvent.ApplicationId,
                jobType,
                ProcessingJobTargetTypes.IntegrationInboundEvent,
                inboundEvent.Id,
                $"integration:{inboundEvent.EventType}:{inboundEvent.IdempotencyKey}",
                inboundEvent.ReceivedAt),
            cancellationToken);
    }

    private async Task<ApplicationEntity> GetApplicationByIdAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken)
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

    private async Task<ApplicationEntity> GetApplicationByCodeAsync(Guid tenantId, string applicationCode, CancellationToken cancellationToken)
    {
        var code = NormalizeKey(applicationCode, "application_code_required", CodeMaxLength, lowerInvariant: true);
        var application = await dbContext.Applications.FirstOrDefaultAsync(
            item =>
                item.TenantId == tenantId &&
                item.Code == code &&
                item.DeletedAt == null &&
                item.Status == ApplicationStatus.Active,
            cancellationToken);

        return application ?? throw new KeyNotFoundException("application_not_found");
    }

    private async Task<IntegrationConnectionEntity> AuthenticateConnectionAsync(
        Guid tenantId,
        Guid applicationId,
        IntegrationAuthenticationRequest authentication,
        CancellationToken cancellationToken)
    {
        var keyId = CleanOptional(authentication.KeyId, SecretReferenceMaxLength);
        var query = dbContext.IntegrationConnections
            .Where(connection =>
                connection.TenantId == tenantId &&
                connection.ApplicationId == applicationId &&
                connection.Status == IntegrationConnectionStatus.Active &&
                connection.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(keyId))
        {
            query = query.Where(connection => connection.SecretReference == keyId);
        }

        var connections = await query.ToListAsync(cancellationToken);
        foreach (var connection in connections)
        {
            if (connection.AuthMode == IntegrationAuthMode.None)
            {
                return connection;
            }

            if (connection.AuthMode == IntegrationAuthMode.InternalApiKey
                && secretHasher.VerifySecret(authentication.ApiKey ?? string.Empty, connection.SecretHash))
            {
                return connection;
            }
        }

        throw new UnauthorizedAccessException("integration_unauthorized");
    }

    private async Task<IntegrationInboundEventEntity?> FindExistingInboundEventAsync(
        Guid tenantId,
        Guid applicationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.IntegrationInboundEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private static IntegrationConnectionResponse ToResponse(IntegrationConnectionEntity connection)
    {
        return new IntegrationConnectionResponse(
            connection.Id,
            connection.TenantId,
            connection.ApplicationId,
            connection.Application?.Code ?? string.Empty,
            connection.Name,
            connection.BaseUrl,
            connection.AuthMode,
            connection.Status,
            connection.SecretReference,
            !string.IsNullOrWhiteSpace(connection.SecretHash),
            connection.SecretRotatedAt,
            connection.MetadataJson,
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    private static IntegrationInboundEventResponse ToResponse(IntegrationInboundEventEntity inboundEvent, string applicationCode, bool isDuplicate)
    {
        return new IntegrationInboundEventResponse(
            inboundEvent.Id,
            inboundEvent.TenantId,
            inboundEvent.ApplicationId,
            applicationCode,
            inboundEvent.IntegrationConnectionId,
            inboundEvent.EventType,
            inboundEvent.IdempotencyKey,
            inboundEvent.ExternalEventId,
            inboundEvent.ObjectType,
            inboundEvent.ExternalObjectId,
            inboundEvent.Status,
            inboundEvent.ReceivedAt,
            isDuplicate);
    }

    private static string ValidateAndSerialize<TRequest>(TRequest request)
    {
        return JsonSerializer.Serialize(request, JsonOptions);
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

    private static string NormalizeBaseUrl(string value)
    {
        var cleaned = CleanRequired(value, "integration_base_url_required", BaseUrlMaxLength).TrimEnd('/');
        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_integration_base_url");
        }

        return cleaned;
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        var cleaned = CleanOptional(value, BaseUrlMaxLength);
        if (cleaned is null)
        {
            return null;
        }

        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_url");
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
