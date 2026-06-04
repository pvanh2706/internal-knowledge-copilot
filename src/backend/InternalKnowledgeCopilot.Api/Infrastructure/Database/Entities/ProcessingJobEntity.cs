using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class ProcessingJobEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid? ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public required string JobType { get; set; }

    public required string TargetType { get; set; }

    public Guid TargetId { get; set; }

    public string? IdempotencyKey { get; set; }

    public ProcessingJobStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorType { get; set; }

    public string? ErrorDetailsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? LastErrorAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }
}
