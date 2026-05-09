using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiFeedbackEntity
{
    public Guid Id { get; set; }

    public Guid AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public Guid UserId { get; set; }

    public UserEntity? User { get; set; }

    public AiFeedbackValue Value { get; set; }

    public string? Note { get; set; }

    public FeedbackReviewStatus ReviewStatus { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public UserEntity? ReviewedByUser { get; set; }

    public string? ReviewerNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
