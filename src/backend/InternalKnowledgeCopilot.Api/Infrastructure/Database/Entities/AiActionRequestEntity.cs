using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiActionRequestEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid RecommendationId { get; set; }

    public AiRecommendationEntity? Recommendation { get; set; }

    public required string ActionType { get; set; }

    public required string TargetObjectType { get; set; }

    public required string TargetExternalObjectId { get; set; }

    public required string PayloadJson { get; set; }

    public string? NormalizedPayloadJson { get; set; }

    public AiActionApprovalMode ApprovalMode { get; set; } = AiActionApprovalMode.Manual;

    public AiActionRequestStatus Status { get; set; } = AiActionRequestStatus.PendingApproval;

    public required string IdempotencyKey { get; set; }

    public Guid? RequestedByUserId { get; set; }

    public UserEntity? RequestedByUser { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public UserEntity? ApprovedByUser { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public UserEntity? RejectedByUser { get; set; }

    public Guid? ExecutedByUserId { get; set; }

    public UserEntity? ExecutedByUser { get; set; }

    public string? RejectionReason { get; set; }

    public string? CancellationReason { get; set; }

    public string? ValidationResultJson { get; set; }

    public string? RuleDecisionJson { get; set; }

    public string? ExternalExecutionId { get; set; }

    public string? ExecutionResultJson { get; set; }

    public string? ExecutionError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public DateTimeOffset? ExecutingStartedAt { get; set; }

    public DateTimeOffset? ExecutedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}
