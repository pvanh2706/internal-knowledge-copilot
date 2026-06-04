using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.WorkflowCopilot;

public sealed record DealStageChangedWorkflowEventRequest(
    Guid ApplicationId,
    string ExternalObjectId,
    string? FromStage,
    string ToStage,
    string? IdempotencyKey,
    DateTimeOffset? OccurredAt,
    string? DealContextJson,
    string? NotesJson,
    string? TasksJson,
    string? EmailsJson,
    string? CallsJson,
    string? RecentActivitiesJson);

public sealed record WorkflowRecommendationResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid DomainEventId,
    Guid WorkflowDefinitionId,
    string ObjectType,
    string ExternalObjectId,
    string Title,
    string Summary,
    IReadOnlyList<string> RecommendedNextSteps,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> ClarificationQuestions,
    IReadOnlyList<string> SuggestedTasks,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> WonLostSignals,
    string ReasoningLabel,
    IReadOnlyList<WorkflowRecommendationSourceResponse> Sources,
    AiRecommendationStatus Status,
    AiRecommendationFeedbackValue? FeedbackValue,
    string? FeedbackNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowRecommendationSourceResponse(
    KnowledgeSourceType SourceType,
    string SourceId,
    Guid? ApplicationId,
    Guid? KnowledgeSourceId,
    Guid? DocumentId,
    Guid? WikiPageId,
    string? ExternalObjectType,
    string? ExternalObjectId,
    string Title,
    string FolderPath,
    string? SectionTitle,
    int? SectionIndex,
    string Excerpt,
    int Rank);

public sealed record WorkflowRecommendationFeedbackRequest(
    AiRecommendationFeedbackValue Value,
    string? Note);
