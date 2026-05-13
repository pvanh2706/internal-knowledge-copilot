using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class EvaluationCaseEntity
{
    public Guid Id { get; set; }

    public Guid? SourceFeedbackId { get; set; }

    public AiFeedbackEntity? SourceFeedback { get; set; }

    public required string Question { get; set; }

    public required string ExpectedAnswer { get; set; }

    public string? ExpectedKeywordsJson { get; set; }

    public AiScopeType ScopeType { get; set; }

    public Guid? FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public Guid? DocumentId { get; set; }

    public DocumentEntity? Document { get; set; }

    public Guid CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
