namespace InternalKnowledgeCopilot.Api.Modules.Operations;

public sealed record OperationalMetricsResponse(
    DateTimeOffset GeneratedAt,
    int PendingSyncJobCount,
    int DeadLetteredJobCount,
    int IndexingFailureCount,
    int UnprocessedInboundSyncEventCount,
    DateTimeOffset? OldestUnprocessedInboundSyncReceivedAt,
    int RecommendationCount,
    double? AverageRecommendationLatencyMs,
    double ActionApprovalRate,
    double ActionExecutionSuccessRate,
    double IncorrectFeedbackRate);
