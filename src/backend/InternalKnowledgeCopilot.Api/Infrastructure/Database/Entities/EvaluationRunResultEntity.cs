namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class EvaluationRunResultEntity
{
    public Guid Id { get; set; }

    public Guid EvaluationRunId { get; set; }

    public EvaluationRunEntity? EvaluationRun { get; set; }

    public Guid EvaluationCaseId { get; set; }

    public EvaluationCaseEntity? EvaluationCase { get; set; }

    public Guid? AiInteractionId { get; set; }

    public AiInteractionEntity? AiInteraction { get; set; }

    public required string ActualAnswer { get; set; }

    public bool Passed { get; set; }

    public double Score { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
