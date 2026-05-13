using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class KnowledgeChunkIndexEntity
{
    public required string ChunkId { get; set; }

    public KnowledgeSourceType SourceType { get; set; }

    public required string SourceId { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? DocumentVersionId { get; set; }

    public Guid? WikiPageId { get; set; }

    public Guid? FolderId { get; set; }

    public required string VisibilityScope { get; set; }

    public required string Status { get; set; }

    public required string Title { get; set; }

    public required string FolderPath { get; set; }

    public string? SectionTitle { get; set; }

    public int? SectionIndex { get; set; }

    public required string Text { get; set; }

    public required string NormalizedText { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
