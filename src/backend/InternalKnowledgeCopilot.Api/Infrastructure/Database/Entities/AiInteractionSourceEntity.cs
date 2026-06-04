using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiInteractionSourceEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public KnowledgeSourceType SourceType { get; set; }

    public required string SourceId { get; set; }

    public Guid? ApplicationId { get; set; }

    public Guid? KnowledgeSourceId { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? DocumentVersionId { get; set; }

    public Guid? WikiPageId { get; set; }

    public Guid? ExternalObjectRecordId { get; set; }

    public string? ExternalObjectType { get; set; }

    public string? ExternalObjectId { get; set; }

    public required string Title { get; set; }

    public required string FolderPath { get; set; }

    public string? SectionTitle { get; set; }

    public required string Excerpt { get; set; }

    public int Rank { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
