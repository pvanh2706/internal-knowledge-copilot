using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Feedback;

public sealed record SubmitFeedbackRequest(AiFeedbackValue Value, string? Note);

public sealed record UpdateFeedbackReviewStatusRequest(FeedbackReviewStatus Status, string? ReviewerNote);

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
    string Excerpt,
    int Rank);
