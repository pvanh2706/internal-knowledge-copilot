using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.WorkflowCopilot;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.WorkflowCopilot;

public sealed class WorkflowCopilotServiceTests
{
    [Fact]
    public async Task HandleDealStageChangedAsync_CreatesStoredRecommendationGroundedBySources()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedTenantApplicationUserAndWorkflowAsync(fixture.DbContext);
        var sourceResult = CreateProcessSourceResult(seed.Tenant.Id, seed.Application.Id, "Proposal checklist", "Proposal must include decision criteria and next customer meeting.");
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, new FakeKnowledgeVectorStore([sourceResult]));

        var response = await service.HandleDealStageChangedAsync(
            seed.User.Id,
            new DealStageChangedWorkflowEventRequest(
                seed.Application.Id,
                "D-100",
                "qualification",
                "proposal",
                "deal-stage-001",
                DateTimeOffset.UtcNow,
                """{"name":"Acme expansion","amount":50000,"decisionCriteria":"security review"}""",
                """[{"body":"Customer asked for implementation plan."}]""",
                """[{"title":"Send proposal","status":"open"}]""",
                null,
                null,
                """[{"type":"meeting","summary":"Discovery completed"}]"""));

        Assert.Equal("deal", response.ObjectType);
        Assert.Equal("D-100", response.ExternalObjectId);
        Assert.NotEmpty(response.RecommendedNextSteps);
        Assert.NotEmpty(response.Sources);
        Assert.Contains("not predictive ML", response.ReasoningLabel, StringComparison.OrdinalIgnoreCase);
        Assert.All(response.WonLostSignals, signal => Assert.Contains("not predictive ML", signal, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await fixture.DbContext.DomainEvents.CountAsync());
        Assert.Equal(1, await fixture.DbContext.AiRecommendations.CountAsync());
        var recommendation = await fixture.DbContext.AiRecommendations.SingleAsync();
        Assert.Contains("Proposal checklist", recommendation.SourcesJson, StringComparison.OrdinalIgnoreCase);
        var domainEvent = await fixture.DbContext.DomainEvents.SingleAsync();
        Assert.Equal(DomainEventStatus.RecommendationCreated, domainEvent.Status);
        Assert.Contains("decisionCriteria", domainEvent.ObjectContextJson, StringComparison.OrdinalIgnoreCase);

        var history = await service.GetRecommendationsAsync(seed.User.Id, seed.Application.Id, "deal", "D-100");
        var historyItem = Assert.Single(history);
        Assert.Equal(response.Id, historyItem.Id);

        var feedback = await service.RecordRecommendationFeedbackAsync(
            response.Id,
            seed.User.Id,
            new WorkflowRecommendationFeedbackRequest(AiRecommendationFeedbackValue.Helpful, "Useful next steps."));
        Assert.Equal(AiRecommendationStatus.FeedbackReceived, feedback.Status);
        Assert.Equal(AiRecommendationFeedbackValue.Helpful, feedback.FeedbackValue);
    }

    [Fact]
    public async Task HandleDealStageChangedAsync_ThrowsWhenWorkflowMissing()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedTenantApplicationAndUserAsync(fixture.DbContext);
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, new FakeKnowledgeVectorStore([]));

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.HandleDealStageChangedAsync(
            seed.User.Id,
            new DealStageChangedWorkflowEventRequest(
                seed.Application.Id,
                "D-100",
                "qualification",
                "proposal",
                "deal-stage-001",
                DateTimeOffset.UtcNow,
                """{"name":"Acme expansion"}""",
                null,
                null,
                null,
                null,
                null)));

        Assert.Equal("workflow_definition_not_found", exception.Message);
        Assert.Empty(fixture.DbContext.DomainEvents);
        Assert.Empty(fixture.DbContext.AiRecommendations);
    }

    [Fact]
    public async Task HandleDealStageChangedAsync_ThrowsWhenObjectContextMissing()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedTenantApplicationUserAndWorkflowAsync(fixture.DbContext);
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, new FakeKnowledgeVectorStore([]));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.HandleDealStageChangedAsync(
            seed.User.Id,
            new DealStageChangedWorkflowEventRequest(
                seed.Application.Id,
                "D-100",
                "qualification",
                "proposal",
                "deal-stage-001",
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                null,
                null)));

        Assert.Equal("object_context_required", exception.Message);
        Assert.Empty(fixture.DbContext.DomainEvents);
        Assert.Empty(fixture.DbContext.AiRecommendations);
    }

    [Fact]
    public async Task HandleDealStageChangedAsync_RejectsWhenExternalObjectPermissionFails()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedTenantApplicationUserAndWorkflowAsync(fixture.DbContext);
        var now = DateTimeOffset.UtcNow;
        fixture.DbContext.ExternalObjects.Add(new ExternalObjectEntity
        {
            Id = Guid.NewGuid(),
            TenantId = seed.Tenant.Id,
            ApplicationId = seed.Application.Id,
            ObjectType = "deal",
            ExternalObjectId = "D-100",
            Title = "Sensitive deal",
            Status = ExternalObjectStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await fixture.DbContext.SaveChangesAsync();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, new FakeKnowledgeVectorStore([]));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HandleDealStageChangedAsync(
            seed.User.Id,
            new DealStageChangedWorkflowEventRequest(
                seed.Application.Id,
                "D-100",
                "qualification",
                "proposal",
                "deal-stage-001",
                DateTimeOffset.UtcNow,
                """{"name":"Acme expansion"}""",
                null,
                null,
                null,
                null,
                null)));

        Assert.Equal("external_object_forbidden", exception.Message);
        Assert.Empty(fixture.DbContext.DomainEvents);
        Assert.Empty(fixture.DbContext.AiRecommendations);
    }

    private static WorkflowCopilotService CreateService(
        AppDbContext dbContext,
        Guid tenantId,
        FakeKnowledgeVectorStore vectorStore,
        IExternalAccessResolver? externalAccessResolver = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant");
        return new WorkflowCopilotService(
            dbContext,
            tenantContext,
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeKeywordIndexService(dbContext),
            new FakeExternalObjectContextClient(),
            externalAccessResolver ?? new AllowingExternalAccessResolver(),
            new MockWorkflowRecommendationGenerationService(),
            new NoopAuditLogService());
    }

    private static async Task<WorkflowSeed> SeedTenantApplicationUserAndWorkflowAsync(AppDbContext dbContext)
    {
        var seed = await SeedTenantApplicationAndUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;
        var workflow = new WorkflowDefinitionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = seed.Tenant.Id,
            ApplicationId = seed.Application.Id,
            Name = "Proposal stage workflow",
            Description = "Guide sales reps when a deal moves into proposal.",
            EventType = "deal.stage.changed",
            ObjectType = "deal",
            TriggerStage = "proposal",
            Status = WorkflowDefinitionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.WorkflowDefinitions.Add(workflow);
        dbContext.WorkflowSteps.Add(new WorkflowStepEntity
        {
            Id = Guid.NewGuid(),
            TenantId = seed.Tenant.Id,
            WorkflowDefinitionId = workflow.Id,
            StepOrder = 1,
            Name = "Validate proposal readiness",
            StepType = WorkflowStepType.Checklist,
            Instruction = "Confirm decision criteria, next meeting, and proposal owner before changing CRM tasks.",
            RetrievalQuery = "proposal stage checklist decision criteria follow-up task",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
        return seed;
    }

    private static async Task<WorkflowSeed> SeedTenantApplicationAndUserAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = $"tenant-{Guid.NewGuid():N}",
            Name = "Tenant",
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var application = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = "crm",
            Name = "CRM",
            ApplicationType = ApplicationType.Crm,
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var team = new TeamEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Sales",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = $"sales-{Guid.NewGuid():N}@example.local",
            DisplayName = "Sales User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = team.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Applications.Add(application);
        dbContext.Teams.Add(team);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return new WorkflowSeed(tenant, application, user, team);
    }

    private static KnowledgeVectorSearchResult CreateProcessSourceResult(Guid tenantId, Guid applicationId, string title, string text)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        return new KnowledgeVectorSearchResult(
            $"chunk-{Guid.NewGuid():N}",
            text,
            new Dictionary<string, object?>
            {
                ["chunk_id"] = $"chunk-{Guid.NewGuid():N}",
                ["tenant_id"] = tenantId.ToString(),
                ["application_id"] = applicationId.ToString(),
                ["source_type"] = "document",
                ["source_id"] = versionId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["document_version_id"] = versionId.ToString(),
                ["visibility_scope"] = "company",
                ["status"] = "approved",
                ["title"] = title,
                ["folder_path"] = "/Sales Playbooks",
                ["section_title"] = "Proposal stage",
                ["section_index"] = 1,
            },
            0.05);
    }

    private sealed class FakeKnowledgeVectorStore(IReadOnlyList<KnowledgeVectorSearchResult> results) : IKnowledgeVectorStore
    {
        public KnowledgeQueryFilter? LastFilter { get; private set; }

        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTenantDataAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(
            float[] embedding,
            int limit,
            KnowledgeQueryFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeExternalObjectContextClient : IExternalObjectContextClient
    {
        public Task<ExternalObjectContextResponse> GetObjectContextAsync(
            ExternalConnectorContext context,
            string objectType,
            string externalObjectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExternalObjectContextResponse(
                objectType,
                externalObjectId,
                """{"name":"Fetched deal context"}""",
                null));
        }
    }

    private sealed class AllowingExternalAccessResolver : IExternalAccessResolver
    {
        public Task<ExternalAccessCheckResponse> CheckAccessAsync(
            ExternalConnectorContext context,
            ExternalAccessCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExternalAccessCheckResponse(true, null, DateTimeOffset.UtcNow));
        }
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private SqliteFixture(SqliteConnection connection, AppDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }

        public AppDbContext DbContext { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
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

    private sealed record WorkflowSeed(TenantEntity Tenant, ApplicationEntity Application, UserEntity User, TeamEntity Team);
}
