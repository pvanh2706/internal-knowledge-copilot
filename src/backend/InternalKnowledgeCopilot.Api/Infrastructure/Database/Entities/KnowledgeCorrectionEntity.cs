using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class KnowledgeCorrectionEntity
{
    public Guid Id { get; set; }

    public Guid QualityIssueId { get; set; }

    public AiQualityIssueEntity? QualityIssue { get; set; }

    public Guid AiFeedbackId { get; set; }

    public AiFeedbackEntity? AiFeedback { get; set; }

    public Guid AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public required string Question { get; set; }

    public required string CorrectionText { get; set; }

    public VisibilityScope VisibilityScope { get; set; }

    public Guid? FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public Guid? DocumentId { get; set; }

    public DocumentEntity? Document { get; set; }

    public KnowledgeCorrectionStatus Status { get; set; }

    public string? RejectReason { get; set; }

    public Guid CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public UserEntity? ApprovedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? IndexedAt { get; set; }
}
