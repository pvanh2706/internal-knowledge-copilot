using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiRecommendationEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid DomainEventId { get; set; }

    public DomainEventEntity? DomainEvent { get; set; }

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinitionEntity? WorkflowDefinition { get; set; }

    public required string ObjectType { get; set; }

    public required string ExternalObjectId { get; set; }

    public required string Title { get; set; }

    public required string Summary { get; set; }

    public required string RecommendedNextStepsJson { get; set; }

    public required string RisksJson { get; set; }

    public required string ClarificationQuestionsJson { get; set; }

    public required string SuggestedTasksJson { get; set; }

    public required string WarningsJson { get; set; }

    public required string WonLostSignalsJson { get; set; }

    public required string ReasoningLabel { get; set; }

    public required string SourcesJson { get; set; }

    public AiRecommendationStatus Status { get; set; } = AiRecommendationStatus.Ready;

    public AiRecommendationFeedbackValue? FeedbackValue { get; set; }

    public string? FeedbackNote { get; set; }

    public Guid? FeedbackByUserId { get; set; }

    public UserEntity? FeedbackByUser { get; set; }

    public DateTimeOffset? FeedbackAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
