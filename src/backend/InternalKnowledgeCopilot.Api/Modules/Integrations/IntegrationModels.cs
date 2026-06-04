using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Integrations;

public sealed record IntegrationAuthenticationRequest(
    string? KeyId,
    string? ApiKey);

public sealed record IntegrationConnectionResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string ApplicationCode,
    string Name,
    string BaseUrl,
    IntegrationAuthMode AuthMode,
    IntegrationConnectionStatus Status,
    string SecretReference,
    bool SecretConfigured,
    DateTimeOffset? SecretRotatedAt,
    string? MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateIntegrationConnectionRequest(
    Guid ApplicationId,
    string Name,
    string BaseUrl,
    IntegrationAuthMode AuthMode,
    string SecretReference,
    string? SecretValue,
    IntegrationConnectionStatus? Status,
    string? MetadataJson);

public sealed record IntegrationInboundEventResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string ApplicationCode,
    Guid IntegrationConnectionId,
    IntegrationInboundEventType EventType,
    string IdempotencyKey,
    string? ExternalEventId,
    string? ObjectType,
    string? ExternalObjectId,
    IntegrationInboundEventStatus Status,
    DateTimeOffset ReceivedAt,
    bool IsDuplicate);

public sealed record DomainIntegrationEventRequest(
    string IdempotencyKey,
    string EventType,
    string? ExternalEventId,
    string? ObjectType,
    string? ExternalObjectId,
    DateTimeOffset? OccurredAt,
    string? PayloadJson,
    string? MetadataJson);

public sealed record DocumentChangedIntegrationRequest(
    string IdempotencyKey,
    string ExternalDocumentId,
    string ChangeType,
    string? KnowledgeSourceExternalId,
    string? Title,
    string? Url,
    string? ContentHash,
    DateTimeOffset? ChangedAt,
    string? MetadataJson);

public sealed record ObjectSyncIntegrationRequest(
    string IdempotencyKey,
    string ObjectType,
    string ExternalObjectId,
    string Title,
    string? KnowledgeSourceExternalId,
    string? Url,
    string? ContentHash,
    string? AclHash,
    DateTimeOffset? SyncedAt,
    string? MetadataJson);

public sealed record PermissionSyncIntegrationRequest(
    string IdempotencyKey,
    string ObjectType,
    string ExternalObjectId,
    IReadOnlyList<PermissionSyncAclSnapshotRequest> AclSnapshots,
    DateTimeOffset? SyncedAt,
    string? MetadataJson);

public sealed record PermissionSyncAclSnapshotRequest(
    string SubjectType,
    string SubjectId,
    string? SubjectDisplayName,
    ExternalAclPermission Permission,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? MetadataJson);
