using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.ActionApprovals;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Integrations;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Tests.BackgroundJobs;

public sealed class ProcessingJobServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task EnqueueAsync_ReturnsExistingJob_WhenIdempotencyKeyMatchesTenantApplicationAndType()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenant, application) = await SeedTenantAndApplicationAsync(fixture.DbContext);
        var tenantContext = CreateTenantContext(tenant);
        var service = CreateService(fixture.DbContext, tenantContext);
        var targetId = Guid.NewGuid();

        var first = await service.EnqueueAsync(new ProcessingJobEnqueueRequest(
            tenant.Id,
            application.Id,
            ProcessingJobTypes.ObjectSync,
            ProcessingJobTargetTypes.IntegrationInboundEvent,
            targetId,
            "object-sync-1"));
        var second = await service.EnqueueAsync(new ProcessingJobEnqueueRequest(
            tenant.Id,
            application.Id,
            ProcessingJobTypes.ObjectSync,
            ProcessingJobTargetTypes.IntegrationInboundEvent,
            targetId,
            "object-sync-1"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await fixture.DbContext.ProcessingJobs.CountAsync());
        Assert.Equal(application.Id, first.ApplicationId);
        Assert.Equal(ProcessingJobStatus.Pending, first.Status);
    }

    [Fact]
    public async Task ClaimNextDueJobAsync_ClaimsOnlyDuePendingJobAndSetsRunningMetadata()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenant, application) = await SeedTenantAndApplicationAsync(fixture.DbContext);
        var tenantContext = CreateTenantContext(tenant);
        var now = DateTimeOffset.UtcNow;
        var dueJob = CreateJob(tenant.Id, application.Id, ProcessingJobTypes.ObjectSync, now.AddMinutes(-1));
        var futureJob = CreateJob(tenant.Id, application.Id, ProcessingJobTypes.PermissionSync, now.AddMinutes(10));
        fixture.DbContext.ProcessingJobs.AddRange(dueJob, futureJob);
        await fixture.DbContext.SaveChangesAsync();
        var service = CreateService(fixture.DbContext, tenantContext);

        var claimed = await service.ClaimNextDueJobAsync(now);

        Assert.NotNull(claimed);
        Assert.Equal(dueJob.Id, claimed.Id);
        Assert.Equal(ProcessingJobStatus.Running, claimed.Status);
        Assert.Equal(1, claimed.Attempts);
        Assert.Equal(now, claimed.LastAttemptAt);
        Assert.Null(await service.ClaimNextDueJobAsync(now));
        Assert.Equal(ProcessingJobStatus.Pending, futureJob.Status);
    }

    [Fact]
    public async Task ProcessNextJobAsync_RetriesThenDeadLettersFailedDocumentJob()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenant, application) = await SeedTenantAndApplicationAsync(fixture.DbContext);
        var tenantContext = CreateTenantContext(tenant);
        var documentProcessor = new FailingDocumentProcessingService("transient_failure");
        var service = CreateService(fixture.DbContext, tenantContext, documentProcessor);
        var job = CreateJob(tenant.Id, application.Id, ProcessingJobTypes.DocumentSync, DateTimeOffset.UtcNow.AddSeconds(-1));
        job.TargetType = ProcessingJobTargetTypes.DocumentVersion;
        fixture.DbContext.ProcessingJobs.Add(job);
        await fixture.DbContext.SaveChangesAsync();

        var processed = await service.ProcessNextJobAsync();

        Assert.True(processed);
        await fixture.DbContext.Entry(job).ReloadAsync();
        Assert.Equal(ProcessingJobStatus.Pending, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal("transient_failure", job.ErrorCode);
        Assert.NotNull(job.ErrorDetailsJson);
        Assert.True(job.ScheduledAt > DateTimeOffset.UtcNow);

        job.Attempts = 3;
        job.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await fixture.DbContext.SaveChangesAsync();

        processed = await service.ProcessNextJobAsync();

        Assert.True(processed);
        await fixture.DbContext.Entry(job).ReloadAsync();
        Assert.Equal(ProcessingJobStatus.DeadLettered, job.Status);
        Assert.Equal(4, job.Attempts);
        Assert.NotNull(job.DeadLetteredAt);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task ProcessNextJobAsync_SetsTenantAndApplicationContextForHandler()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenant, application) = await SeedTenantAndApplicationAsync(fixture.DbContext);
        var tenantContext = CreateTenantContext(tenant);
        var documentProcessor = new RecordingDocumentProcessingService(tenantContext, tenant.Id, application.Id);
        var service = CreateService(fixture.DbContext, tenantContext, documentProcessor);
        var job = CreateJob(tenant.Id, application.Id, ProcessingJobTypes.DocumentSync, DateTimeOffset.UtcNow.AddSeconds(-1));
        job.TargetType = ProcessingJobTargetTypes.DocumentVersion;
        fixture.DbContext.ProcessingJobs.Add(job);
        await fixture.DbContext.SaveChangesAsync();

        await service.ProcessNextJobAsync();

        await fixture.DbContext.Entry(job).ReloadAsync();
        Assert.True(documentProcessor.WasCalled);
        Assert.Equal(ProcessingJobStatus.Succeeded, job.Status);
        Assert.Equal(tenant.Id, tenantContext.TenantId);
        Assert.Equal(application.Id, tenantContext.ApplicationId);
    }

    [Fact]
    public async Task ProcessNextJobAsync_DeadLettersJobWhenApplicationDoesNotBelongToTenant()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenantA, _) = await SeedTenantAndApplicationAsync(fixture.DbContext, "tenant-a", "crm-a");
        var (tenantB, applicationB) = await SeedTenantAndApplicationAsync(fixture.DbContext, "tenant-b", "crm-b");
        var tenantContext = CreateTenantContext(tenantA);
        var documentProcessor = new RecordingDocumentProcessingService(tenantContext, tenantA.Id, applicationB.Id);
        var service = CreateService(fixture.DbContext, tenantContext, documentProcessor);
        var job = CreateJob(tenantA.Id, applicationB.Id, ProcessingJobTypes.DocumentSync, DateTimeOffset.UtcNow.AddSeconds(-1));
        job.TargetType = ProcessingJobTargetTypes.DocumentVersion;
        fixture.DbContext.ProcessingJobs.Add(job);
        await fixture.DbContext.SaveChangesAsync();

        await service.ProcessNextJobAsync();

        await fixture.DbContext.Entry(job).ReloadAsync();
        Assert.Equal(tenantB.Id, applicationB.TenantId);
        Assert.False(documentProcessor.WasCalled);
        Assert.Equal(ProcessingJobStatus.DeadLettered, job.Status);
        Assert.Equal("application_not_found", job.ErrorCode);
    }

    [Fact]
    public async Task ProcessNextJobAsync_AppliesObjectAndPermissionSyncEvents()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (tenant, application) = await SeedTenantAndApplicationAsync(fixture.DbContext);
        var connection = await SeedConnectionAsync(fixture.DbContext, tenant.Id, application.Id);
        var tenantContext = CreateTenantContext(tenant);
        var service = CreateService(fixture.DbContext, tenantContext);
        var now = DateTimeOffset.UtcNow;
        var objectEvent = new IntegrationInboundEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            IntegrationConnectionId = connection.Id,
            EventType = IntegrationInboundEventType.ObjectSync,
            IdempotencyKey = "object-1",
            ObjectType = "deal",
            ExternalObjectId = "D-100",
            PayloadJson = JsonSerializer.Serialize(
                new ObjectSyncIntegrationRequest(
                    "object-1",
                    "deal",
                    "D-100",
                    "Big Renewal",
                    "crm-deals",
                    "https://crm.example/deals/D-100",
                    "content-hash",
                    "acl-hash",
                    now,
                    "{\"stage\":\"proposal\"}"),
                JsonOptions),
            Status = IntegrationInboundEventStatus.Received,
            ReceivedAt = now,
            CreatedAt = now,
        };
        fixture.DbContext.IntegrationInboundEvents.Add(objectEvent);
        await fixture.DbContext.SaveChangesAsync();
        await service.EnqueueAsync(new ProcessingJobEnqueueRequest(
            tenant.Id,
            application.Id,
            ProcessingJobTypes.ObjectSync,
            ProcessingJobTargetTypes.IntegrationInboundEvent,
            objectEvent.Id,
            "object-sync-1",
            now.AddSeconds(-1)));

        await service.ProcessNextJobAsync();

        await fixture.DbContext.Entry(objectEvent).ReloadAsync();
        var externalObject = await fixture.DbContext.ExternalObjects.SingleAsync();
        Assert.Equal(IntegrationInboundEventStatus.Processed, objectEvent.Status);
        Assert.Equal(application.Id, externalObject.ApplicationId);
        Assert.Equal("deal", externalObject.ObjectType);
        Assert.Equal("D-100", externalObject.ExternalObjectId);
        Assert.Equal("Big Renewal", externalObject.Title);

        var permissionEvent = new IntegrationInboundEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            IntegrationConnectionId = connection.Id,
            EventType = IntegrationInboundEventType.PermissionSync,
            IdempotencyKey = "permission-1",
            ObjectType = "deal",
            ExternalObjectId = "D-100",
            PayloadJson = JsonSerializer.Serialize(
                new PermissionSyncIntegrationRequest(
                    "permission-1",
                    "deal",
                    "D-100",
                    [
                        new PermissionSyncAclSnapshotRequest(
                            "user",
                            "seller-1",
                            "Seller One",
                            ExternalAclPermission.View,
                            null,
                            null,
                            null)
                    ],
                    now,
                    null),
                JsonOptions),
            Status = IntegrationInboundEventStatus.Received,
            ReceivedAt = now,
            CreatedAt = now,
        };
        fixture.DbContext.IntegrationInboundEvents.Add(permissionEvent);
        await fixture.DbContext.SaveChangesAsync();
        await service.EnqueueAsync(new ProcessingJobEnqueueRequest(
            tenant.Id,
            application.Id,
            ProcessingJobTypes.PermissionSync,
            ProcessingJobTargetTypes.IntegrationInboundEvent,
            permissionEvent.Id,
            "permission-sync-1",
            now.AddSeconds(-1)));

        await service.ProcessNextJobAsync();

        await fixture.DbContext.Entry(permissionEvent).ReloadAsync();
        await fixture.DbContext.Entry(externalObject).ReloadAsync();
        var snapshot = await fixture.DbContext.ExternalAclSnapshots.SingleAsync();
        Assert.Equal(IntegrationInboundEventStatus.Processed, permissionEvent.Status);
        Assert.Equal(application.Id, snapshot.ApplicationId);
        Assert.Equal(externalObject.Id, snapshot.ExternalObjectRecordId);
        Assert.Equal("seller-1", snapshot.SubjectId);
        Assert.NotNull(externalObject.AclSyncedAt);
    }

    private static ProcessingJobService CreateService(
        AppDbContext dbContext,
        TenantContext tenantContext,
        IDocumentProcessingService? documentProcessingService = null)
    {
        return new ProcessingJobService(
            dbContext,
            tenantContext,
            documentProcessingService ?? new RecordingDocumentProcessingService(tenantContext, Guid.Empty, null),
            new NoopFeedbackService(),
            new KnowledgeSourceService(dbContext, tenantContext, new NoopAuditLogService()),
            new NoopKnowledgeIndexRebuildService(),
            new NoopActionApprovalService(),
            Options.Create(new BackgroundJobOptions()),
            NullLogger<ProcessingJobService>.Instance);
    }

    private static ProcessingJobEntity CreateJob(Guid tenantId, Guid? applicationId, string jobType, DateTimeOffset scheduledAt)
    {
        return new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            JobType = jobType,
            TargetType = ProcessingJobTargetTypes.IntegrationInboundEvent,
            TargetId = Guid.NewGuid(),
            Status = ProcessingJobStatus.Pending,
            Attempts = 0,
            CreatedAt = scheduledAt.AddMinutes(-1),
            ScheduledAt = scheduledAt,
        };
    }

    private static TenantContext CreateTenantContext(TenantEntity tenant)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenant.Id, tenant.Code);
        return tenantContext;
    }

    private static async Task<(TenantEntity Tenant, ApplicationEntity Application)> SeedTenantAndApplicationAsync(
        AppDbContext dbContext,
        string tenantCode = "acme",
        string applicationCode = "crm")
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = tenantCode,
            Code = tenantCode,
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var application = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = applicationCode,
            Name = applicationCode,
            ApplicationType = ApplicationType.Internal,
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Tenants.Add(tenant);
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync();
        return (tenant, application);
    }

    private static async Task<IntegrationConnectionEntity> SeedConnectionAsync(AppDbContext dbContext, Guid tenantId, Guid applicationId)
    {
        var now = DateTimeOffset.UtcNow;
        var connection = new IntegrationConnectionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = "CRM",
            BaseUrl = "https://crm.example",
            AuthMode = IntegrationAuthMode.None,
            Status = IntegrationConnectionStatus.Active,
            SecretReference = "crm",
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.IntegrationConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        return connection;
    }

    private sealed class RecordingDocumentProcessingService(TenantContext tenantContext, Guid expectedTenantId, Guid? expectedApplicationId) : IDocumentProcessingService
    {
        public bool WasCalled { get; private set; }

        public Task ProcessDocumentVersionAsync(Guid documentVersionId, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            if (expectedTenantId != Guid.Empty)
            {
                Assert.Equal(expectedTenantId, tenantContext.TenantId);
            }

            if (expectedApplicationId is not null)
            {
                Assert.Equal(expectedApplicationId, tenantContext.ApplicationId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FailingDocumentProcessingService(string errorMessage) : IDocumentProcessingService
    {
        public Task ProcessDocumentVersionAsync(Guid documentVersionId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private sealed class NoopFeedbackService : IAiFeedbackService
    {
        public Task<FeedbackResponse> SubmitAsync(Guid interactionId, Guid userId, SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<IncorrectFeedbackResponse>> GetIncorrectAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeedbackResponse> UpdateReviewStatusAsync(Guid feedbackId, Guid reviewerId, UpdateFeedbackReviewStatusRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<QualityIssueResponse>> GetQualityIssuesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ClassifyIssueAsync(Guid issueId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<KnowledgeCorrectionResponse> CreateCorrectionAsync(Guid issueId, Guid reviewerId, CreateCorrectionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<KnowledgeCorrectionResponse> ApproveCorrectionAsync(Guid correctionId, Guid reviewerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<KnowledgeCorrectionResponse> RejectCorrectionAsync(Guid correctionId, Guid reviewerId, RejectCorrectionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopKnowledgeIndexRebuildService : IKnowledgeIndexRebuildService
    {
        public Task<KnowledgeIndexSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RebuildKnowledgeIndexResponse> RebuildAsync(Guid? actorUserId, RebuildKnowledgeIndexRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopActionApprovalService : IActionApprovalService
    {
        public Task<AiActionRequestResponse> CreateActionAsync(Guid recommendationId, Guid userId, CreateAiActionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AiActionRequestResponse>> GetActionsAsync(Guid userId, Guid? applicationId, AiActionRequestStatus? status, Guid? recommendationId, string? objectType, string? externalObjectId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiActionRequestResponse> ApproveActionAsync(Guid actionRequestId, Guid userId, ApproveAiActionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiActionRequestResponse> RejectActionAsync(Guid actionRequestId, Guid userId, RejectAiActionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiActionRequestResponse> CancelActionAsync(Guid actionRequestId, Guid userId, CancelAiActionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiActionRequestResponse> ExecuteActionAsync(Guid actionRequestId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private SqliteFixture(SqliteConnection connection, AppDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        private SqliteConnection Connection { get; }

        public AppDbContext DbContext { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new SqliteFixture(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
