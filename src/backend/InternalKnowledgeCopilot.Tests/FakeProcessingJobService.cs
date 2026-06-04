using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Tests;

public sealed class FakeProcessingJobService : IProcessingJobService
{
    public List<ProcessingJobEntity> EnqueuedJobs { get; } = [];

    public Task<ProcessingJobEntity> EnqueueAsync(ProcessingJobEnqueueRequest request, CancellationToken cancellationToken = default)
    {
        var job = new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            JobType = request.JobType,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
            ScheduledAt = request.ScheduledAt ?? DateTimeOffset.UtcNow,
        };
        EnqueuedJobs.Add(job);
        return Task.FromResult(job);
    }

    public Task<ProcessingJobEntity?> ClaimNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProcessingJobEntity?>(null);
    }

    public Task<bool> ProcessNextJobAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
