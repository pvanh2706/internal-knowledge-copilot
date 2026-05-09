using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class ProcessingJobEntity
{
    public Guid Id { get; set; }

    public required string JobType { get; set; }

    public required string TargetType { get; set; }

    public Guid TargetId { get; set; }

    public ProcessingJobStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
