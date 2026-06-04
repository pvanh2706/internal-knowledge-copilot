using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class KnowledgeSourceEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public KnowledgeSourceKind SourceType { get; set; }

    public required string ExternalSourceId { get; set; }

    public required string Name { get; set; }

    public KnowledgeSourceSyncMode SyncMode { get; set; }

    public KnowledgeSourceStatus Status { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset? LastSyncStartedAt { get; set; }

    public DateTimeOffset? LastSyncCompletedAt { get; set; }

    public string? LastSyncStatus { get; set; }

    public string? LastSyncError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
