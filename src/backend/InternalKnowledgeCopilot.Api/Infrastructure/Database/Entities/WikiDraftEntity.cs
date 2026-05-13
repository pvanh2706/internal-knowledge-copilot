using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class WikiDraftEntity
{
    public Guid Id { get; set; }

    public Guid SourceDocumentId { get; set; }

    public DocumentEntity? SourceDocument { get; set; }

    public Guid SourceDocumentVersionId { get; set; }

    public DocumentVersionEntity? SourceDocumentVersion { get; set; }

    public required string Title { get; set; }

    public required string Content { get; set; }

    public required string Language { get; set; }

    public string? MissingInformationJson { get; set; }

    public string? RelatedDocumentsJson { get; set; }

    public WikiStatus Status { get; set; }

    public string? RejectReason { get; set; }

    public Guid GeneratedByUserId { get; set; }

    public UserEntity? GeneratedByUser { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public UserEntity? ReviewedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
