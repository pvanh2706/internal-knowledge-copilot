namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class EvaluationRunEntity
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public int TotalCases { get; set; }

    public int PassedCases { get; set; }

    public int FailedCases { get; set; }

    public Guid CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<EvaluationRunResultEntity> Results { get; set; } = [];
}
