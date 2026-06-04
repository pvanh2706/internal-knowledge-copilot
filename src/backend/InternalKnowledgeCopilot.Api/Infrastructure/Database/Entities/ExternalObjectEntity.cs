using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class ExternalObjectEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid? KnowledgeSourceId { get; set; }

    public KnowledgeSourceEntity? KnowledgeSource { get; set; }

    public required string ObjectType { get; set; }

    public required string ExternalObjectId { get; set; }

    public required string Title { get; set; }

    public string? Url { get; set; }

    public string? MetadataJson { get; set; }

    public string? ContentHash { get; set; }

    public string? AclHash { get; set; }

    public ExternalObjectStatus Status { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public DateTimeOffset? ContentSyncedAt { get; set; }

    public DateTimeOffset? AclSyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
