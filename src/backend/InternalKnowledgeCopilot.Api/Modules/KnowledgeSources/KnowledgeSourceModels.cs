using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;

public sealed record KnowledgeSourceResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string ApplicationCode,
    KnowledgeSourceKind SourceType,
    string ExternalSourceId,
    string Name,
    KnowledgeSourceSyncMode SyncMode,
    KnowledgeSourceStatus Status,
    string? MetadataJson,
    DateTimeOffset? LastSyncStartedAt,
    DateTimeOffset? LastSyncCompletedAt,
    string? LastSyncStatus,
    string? LastSyncError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpsertKnowledgeSourceRequest(
    Guid ApplicationId,
    KnowledgeSourceKind SourceType,
    string? ExternalSourceId,
    string Name,
    KnowledgeSourceSyncMode SyncMode,
    KnowledgeSourceStatus? Status,
    string? MetadataJson,
    DateTimeOffset? LastSyncStartedAt,
    DateTimeOffset? LastSyncCompletedAt,
    string? LastSyncStatus,
    string? LastSyncError);

public sealed record ExternalObjectResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string ApplicationCode,
    Guid? KnowledgeSourceId,
    string ObjectType,
    string ExternalObjectId,
    string Title,
    string? Url,
    string? MetadataJson,
    string? ContentHash,
    string? AclHash,
    ExternalObjectStatus Status,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? ContentSyncedAt,
    DateTimeOffset? AclSyncedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpsertExternalObjectRequest(
    Guid ApplicationId,
    Guid? KnowledgeSourceId,
    string ObjectType,
    string ExternalObjectId,
    string Title,
    string? Url,
    string? MetadataJson,
    string? ContentHash,
    string? AclHash,
    ExternalObjectStatus? Status,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? ContentSyncedAt,
    DateTimeOffset? AclSyncedAt);

public sealed record ReplaceExternalAclSnapshotsRequest(
    Guid ApplicationId,
    string ObjectType,
    string ExternalObjectId,
    IReadOnlyList<ExternalAclSnapshotItemRequest> AclSnapshots);

public sealed record ExternalAclSnapshotItemRequest(
    string SubjectType,
    string SubjectId,
    string? SubjectDisplayName,
    ExternalAclPermission Permission,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? MetadataJson);

public sealed record ExternalAclSnapshotResponse(
    Guid Id,
    Guid ExternalObjectRecordId,
    string ObjectType,
    string ExternalObjectId,
    string SubjectType,
    string SubjectId,
    string? SubjectDisplayName,
    ExternalAclPermission Permission,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? MetadataJson,
    DateTimeOffset SyncedAt);
