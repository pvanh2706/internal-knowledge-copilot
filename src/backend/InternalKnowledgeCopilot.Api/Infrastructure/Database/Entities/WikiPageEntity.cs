using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class WikiPageEntity
{
    public Guid Id { get; set; }

    public Guid SourceDraftId { get; set; }

    public WikiDraftEntity? SourceDraft { get; set; }

    public Guid SourceDocumentId { get; set; }

    public DocumentEntity? SourceDocument { get; set; }

    public Guid SourceDocumentVersionId { get; set; }

    public DocumentVersionEntity? SourceDocumentVersion { get; set; }

    public required string Title { get; set; }

    public required string Content { get; set; }

    public required string Language { get; set; }

    public VisibilityScope VisibilityScope { get; set; }

    public Guid? FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public bool IsCompanyPublicConfirmed { get; set; }

    public Guid PublishedByUserId { get; set; }

    public UserEntity? PublishedByUser { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
