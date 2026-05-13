using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Feedback;

public sealed record SubmitFeedbackRequest(AiFeedbackValue Value, string? Note);

public sealed record UpdateFeedbackReviewStatusRequest(FeedbackReviewStatus Status, string? ReviewerNote);

public sealed record CreateCorrectionRequest(
    string CorrectionText,
    VisibilityScope VisibilityScope,
    Guid? FolderId,
    bool IsCompanyPublicConfirmed);

public sealed record RejectCorrectionRequest(string Reason);

public sealed record FeedbackResponse(
    Guid Id,
    Guid AiInteractionId,
    AiFeedbackValue Value,
    string? Note,
    FeedbackReviewStatus ReviewStatus,
    string? ReviewerNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record IncorrectFeedbackResponse(
    Guid Id,
    Guid AiInteractionId,
    string UserDisplayName,
    string Question,
    string Answer,
    string? Note,
    FeedbackReviewStatus ReviewStatus,
    string? ReviewerNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FeedbackSourceResponse> Sources);

public sealed record FeedbackSourceResponse(
    KnowledgeSourceType SourceType,
    string Title,
    string FolderPath,
    string? SectionTitle,
    string Excerpt,
    int Rank);

public sealed record QualityIssueResponse(
    Guid Id,
    Guid FeedbackId,
    Guid AiInteractionId,
    string Question,
    string Answer,
    string? UserNote,
    AiQualityIssueStatus Status,
    string? FailureType,
    string? Severity,
    string? RootCauseHypothesis,
    IReadOnlyList<string> RecommendedActions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<KnowledgeCorrectionResponse> Corrections);

public sealed record KnowledgeCorrectionResponse(
    Guid Id,
    Guid QualityIssueId,
    string Question,
    string CorrectionText,
    VisibilityScope VisibilityScope,
    Guid? FolderId,
    Guid? DocumentId,
    KnowledgeCorrectionStatus Status,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ApprovedAt);
