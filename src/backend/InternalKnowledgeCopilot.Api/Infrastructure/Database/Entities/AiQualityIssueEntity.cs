using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiQualityIssueEntity
{
    public Guid Id { get; set; }

    public Guid AiFeedbackId { get; set; }

    public AiFeedbackEntity? AiFeedback { get; set; }

    public Guid AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public AiQualityIssueStatus Status { get; set; }

    public string? FailureType { get; set; }

    public string? Severity { get; set; }

    public string? RootCauseHypothesis { get; set; }

    public string? RecommendedActionsJson { get; set; }

    public string? EvidenceJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ClassifiedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
