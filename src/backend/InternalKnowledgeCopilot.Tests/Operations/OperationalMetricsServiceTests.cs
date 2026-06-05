using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Operations;

public sealed class OperationalMetricsServiceTests
{
    [Fact]
    public async Task GetAsync_ComputesTenantScopedOperationalMetrics()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.ProcessingJobs.AddRange(
            new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobType = ProcessingJobTypes.ObjectSync,
                TargetType = ProcessingJobTargetTypes.IntegrationInboundEvent,
                TargetId = Guid.NewGuid(),
                Status = ProcessingJobStatus.Pending,
                CreatedAt = now,
                ScheduledAt = now,
            },
            new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobType = ProcessingJobTypes.ActionExecution,
                TargetType = ProcessingJobTargetTypes.AiActionRequest,
                TargetId = Guid.NewGuid(),
                Status = ProcessingJobStatus.DeadLettered,
                CreatedAt = now,
                ScheduledAt = now,
            },
            new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                JobType = ProcessingJobTypes.ObjectSync,
                TargetType = ProcessingJobTargetTypes.IntegrationInboundEvent,
                TargetId = Guid.NewGuid(),
                Status = ProcessingJobStatus.Pending,
                CreatedAt = now,
                ScheduledAt = now,
            });
        dbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = Guid.NewGuid(),
            OriginalFileName = "failed.pdf",
            StoredFilePath = "failed.pdf",
            FileExtension = ".pdf",
            Status = DocumentVersionStatus.ProcessingFailed,
            UploadedByUserId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.IntegrationInboundEvents.Add(new IntegrationInboundEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            IntegrationConnectionId = Guid.NewGuid(),
            EventType = IntegrationInboundEventType.ObjectSync,
            IdempotencyKey = "sync-1",
            Status = IntegrationInboundEventStatus.Received,
            ReceivedAt = now.AddMinutes(-10),
            CreatedAt = now.AddMinutes(-10),
        });
        dbContext.AiRecommendations.AddRange(
            CreateRecommendation(tenantId, latencyMs: 100),
            CreateRecommendation(tenantId, latencyMs: 300));
        dbContext.AiActionRequests.AddRange(
            CreateAction(tenantId, AiActionRequestStatus.Succeeded),
            CreateAction(tenantId, AiActionRequestStatus.Failed),
            CreateAction(tenantId, AiActionRequestStatus.PendingApproval));
        dbContext.AiFeedback.AddRange(
            CreateFeedback(tenantId, AiFeedbackValue.Incorrect),
            CreateFeedback(tenantId, AiFeedbackValue.Correct));
        await dbContext.SaveChangesAsync();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant");
        var service = new OperationalMetricsService(dbContext, tenantContext);

        var metrics = await service.GetAsync();

        Assert.Equal(1, metrics.PendingSyncJobCount);
        Assert.Equal(1, metrics.DeadLetteredJobCount);
        Assert.Equal(1, metrics.IndexingFailureCount);
        Assert.Equal(1, metrics.UnprocessedInboundSyncEventCount);
        Assert.Equal(2, metrics.RecommendationCount);
        Assert.Equal(200, metrics.AverageRecommendationLatencyMs);
        Assert.Equal(100, metrics.ActionApprovalRate);
        Assert.Equal(50, metrics.ActionExecutionSuccessRate);
        Assert.Equal(50, metrics.IncorrectFeedbackRate);
    }

    private static AiRecommendationEntity CreateRecommendation(Guid tenantId, int latencyMs)
    {
        return new AiRecommendationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            DomainEventId = Guid.NewGuid(),
            WorkflowDefinitionId = Guid.NewGuid(),
            ObjectType = "deal",
            ExternalObjectId = Guid.NewGuid().ToString("N"),
            Title = "Recommendation",
            Summary = "Summary",
            RecommendedNextStepsJson = "[]",
            RisksJson = "[]",
            ClarificationQuestionsJson = "[]",
            SuggestedTasksJson = "[]",
            WarningsJson = "[]",
            WonLostSignalsJson = "[]",
            ReasoningLabel = "Reasoning-based signal, not predictive ML.",
            SourcesJson = "[]",
            LatencyMs = latencyMs,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static AiActionRequestEntity CreateAction(Guid tenantId, AiActionRequestStatus status)
    {
        return new AiActionRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            RecommendationId = Guid.NewGuid(),
            ActionType = "create_task",
            TargetObjectType = "deal",
            TargetExternalObjectId = Guid.NewGuid().ToString("N"),
            PayloadJson = "{}",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static AiFeedbackEntity CreateFeedback(Guid tenantId, AiFeedbackValue value)
    {
        return new AiFeedbackEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AiInteractionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Value = value,
            ReviewStatus = value == AiFeedbackValue.Incorrect ? FeedbackReviewStatus.New : FeedbackReviewStatus.Resolved,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
