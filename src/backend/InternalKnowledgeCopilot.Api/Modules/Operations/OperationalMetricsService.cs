using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Operations;

public interface IOperationalMetricsService
{
    Task<OperationalMetricsResponse> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class OperationalMetricsService(AppDbContext dbContext, ITenantContext tenantContext) : IOperationalMetricsService
{
    public async Task<OperationalMetricsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var syncJobTypes = new[]
        {
            ProcessingJobTypes.DocumentSync,
            ProcessingJobTypes.LegacyExtractAndEmbedDocument,
            ProcessingJobTypes.ObjectSync,
            ProcessingJobTypes.PermissionSync,
            ProcessingJobTypes.WorkflowRecommendation,
            ProcessingJobTypes.ActionExecution,
            ProcessingJobTypes.IndexRebuild
        };

        var pendingSyncJobCount = await dbContext.ProcessingJobs
            .AsNoTracking()
            .CountAsync(job =>
                job.TenantId == tenantId &&
                job.Status == ProcessingJobStatus.Pending &&
                syncJobTypes.Contains(job.JobType),
                cancellationToken);
        var deadLetteredJobCount = await dbContext.ProcessingJobs
            .AsNoTracking()
            .CountAsync(job =>
                job.TenantId == tenantId &&
                job.Status == ProcessingJobStatus.DeadLettered,
                cancellationToken);
        var indexingFailureCount = await dbContext.DocumentVersions
            .AsNoTracking()
            .CountAsync(version =>
                version.TenantId == tenantId &&
                version.Status == DocumentVersionStatus.ProcessingFailed,
                cancellationToken);
        var unprocessedInboundEvents = await dbContext.IntegrationInboundEvents
            .AsNoTracking()
            .Where(inboundEvent =>
                inboundEvent.TenantId == tenantId &&
                (inboundEvent.EventType == IntegrationInboundEventType.ObjectSync ||
                    inboundEvent.EventType == IntegrationInboundEventType.PermissionSync ||
                    inboundEvent.EventType == IntegrationInboundEventType.DocumentChanged) &&
                inboundEvent.Status != IntegrationInboundEventStatus.Processed &&
                inboundEvent.Status != IntegrationInboundEventStatus.Ignored)
            .ToListAsync(cancellationToken);
        var recommendations = await dbContext.AiRecommendations
            .AsNoTracking()
            .Where(recommendation => recommendation.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var actions = await dbContext.AiActionRequests
            .AsNoTracking()
            .Where(action => action.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var feedback = await dbContext.AiFeedback
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var approvalCandidates = actions
            .Count(action => action.Status is not (AiActionRequestStatus.Draft or AiActionRequestStatus.PendingApproval));
        var approvedActions = actions
            .Count(action => action.Status is AiActionRequestStatus.Approved or AiActionRequestStatus.Executing or AiActionRequestStatus.Succeeded or AiActionRequestStatus.Failed);
        var executionCandidates = actions
            .Count(action => action.Status is AiActionRequestStatus.Succeeded or AiActionRequestStatus.Failed);
        var successfulExecutions = actions.Count(action => action.Status == AiActionRequestStatus.Succeeded);
        var incorrectFeedback = feedback.Count(item => item.Value == AiFeedbackValue.Incorrect);

        return new OperationalMetricsResponse(
            DateTimeOffset.UtcNow,
            pendingSyncJobCount,
            deadLetteredJobCount,
            indexingFailureCount,
            unprocessedInboundEvents.Count,
            unprocessedInboundEvents
                .OrderBy(inboundEvent => inboundEvent.ReceivedAt)
                .Select(inboundEvent => (DateTimeOffset?)inboundEvent.ReceivedAt)
                .FirstOrDefault(),
            recommendations.Count,
            recommendations.Count == 0
                ? null
                : Math.Round(recommendations.Average(recommendation => recommendation.LatencyMs), 2),
            approvalCandidates == 0 ? 0 : Math.Round((double)approvedActions / approvalCandidates * 100, 2),
            executionCandidates == 0 ? 0 : Math.Round((double)successfulExecutions / executionCandidates * 100, 2),
            feedback.Count == 0 ? 0 : Math.Round((double)incorrectFeedback / feedback.Count * 100, 2));
    }
}
