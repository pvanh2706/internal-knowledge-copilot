using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.ActionApprovals;

public sealed record CreateAiActionRequest(
    string ActionType,
    string? TargetObjectType,
    string? TargetExternalObjectId,
    string PayloadJson,
    AiActionApprovalMode? ApprovalMode,
    string? IdempotencyKey,
    bool CreateAsDraft = false);

public sealed record ApproveAiActionRequest(string? Note);

public sealed record RejectAiActionRequest(string Reason);

public sealed record CancelAiActionRequest(string? Reason);

public sealed record AiActionRequestResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid RecommendationId,
    string ActionType,
    string TargetObjectType,
    string TargetExternalObjectId,
    string PayloadJson,
    string? NormalizedPayloadJson,
    AiActionApprovalMode ApprovalMode,
    AiActionRequestStatus Status,
    string IdempotencyKey,
    Guid? RequestedByUserId,
    Guid? ApprovedByUserId,
    Guid? RejectedByUserId,
    Guid? ExecutedByUserId,
    string? RejectionReason,
    string? CancellationReason,
    string? ValidationResultJson,
    string? RuleDecisionJson,
    string? ExternalExecutionId,
    string? ExecutionResultJson,
    string? ExecutionError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? ExecutingStartedAt,
    DateTimeOffset? ExecutedAt,
    DateTimeOffset? CancelledAt);
