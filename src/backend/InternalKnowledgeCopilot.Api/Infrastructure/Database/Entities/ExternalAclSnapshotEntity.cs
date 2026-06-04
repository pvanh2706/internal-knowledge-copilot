using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class ExternalAclSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid ExternalObjectRecordId { get; set; }

    public ExternalObjectEntity? ExternalObject { get; set; }

    public required string ObjectType { get; set; }

    public required string ExternalObjectId { get; set; }

    public required string SubjectType { get; set; }

    public required string SubjectId { get; set; }

    public string? SubjectDisplayName { get; set; }

    public ExternalAclPermission Permission { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }

    public DateTimeOffset? ValidTo { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}
