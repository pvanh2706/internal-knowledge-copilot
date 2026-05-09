using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiInteractionSourceEntity
{
    public Guid Id { get; set; }

    public Guid AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public KnowledgeSourceType SourceType { get; set; }

    public required string SourceId { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? DocumentVersionId { get; set; }

    public Guid? WikiPageId { get; set; }

    public required string Title { get; set; }

    public required string FolderPath { get; set; }

    public required string Excerpt { get; set; }

    public int Rank { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
