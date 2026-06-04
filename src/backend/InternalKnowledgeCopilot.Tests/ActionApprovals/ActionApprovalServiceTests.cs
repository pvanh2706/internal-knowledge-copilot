using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.ActionApprovals;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.ActionApprovals;

public sealed class ActionApprovalServiceTests
{
    [Fact]
    public async Task ApprovalLifecycle_ValidatesApprovesAndExecutesAction()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var auditLog = new CapturingAuditLogService();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor, auditLog);

        var created = await service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"Follow up with customer","dueDate":"2026-06-10"}""",
                AiActionApprovalMode.Manual,
                "action-001"));
        var approved = await service.ApproveActionAsync(created.Id, seed.User.Id, new ApproveAiActionRequest("Looks safe."));
        var executed = await service.ExecuteActionAsync(created.Id, seed.User.Id);

        Assert.Equal(AiActionRequestStatus.PendingApproval, created.Status);
        Assert.Equal(AiActionRequestStatus.Approved, approved.Status);
        Assert.Equal(AiActionRequestStatus.Succeeded, executed.Status);
        Assert.Equal("external-action-001", executed.ExternalExecutionId);
        Assert.Equal(3, executor.ValidateCalls);
        Assert.Equal(1, executor.ExecuteCalls);
        Assert.Contains("normalized", executed.NormalizedPayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AiActionRequestStatus.Succeeded, (await fixture.DbContext.AiActionRequests.SingleAsync()).Status);
        Assert.Contains(auditLog.Actions, action => action == "AiActionRequestCreated");
        Assert.Contains(auditLog.Actions, action => action == "AiActionRequestApproved");
        Assert.Contains(auditLog.Actions, action => action == "AiActionRequestExecuting");
        Assert.Contains(auditLog.Actions, action => action == "AiActionRequestSucceeded");
    }

    [Fact]
    public async Task CreateActionAsync_AutoApprovesSafeRuleTask()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);

        var action = await service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"Schedule follow-up"}""",
                AiActionApprovalMode.Rule,
                "rule-action-001"));

        Assert.Equal(AiActionRequestStatus.Approved, action.Status);
        Assert.NotNull(action.ApprovedAt);
        Assert.Contains("rule_auto_approved_safe_task_creation", action.RuleDecisionJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
    }

    [Fact]
    public async Task ExecuteActionAsync_DoesNotExecuteTwiceAfterSuccess()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);

        var created = await service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"Follow up once"}""",
                AiActionApprovalMode.Manual,
                "action-once"));
        await service.ApproveActionAsync(created.Id, seed.User.Id, new ApproveAiActionRequest(null));

        var first = await service.ExecuteActionAsync(created.Id, seed.User.Id);
        var second = await service.ExecuteActionAsync(created.Id, seed.User.Id);

        Assert.Equal(AiActionRequestStatus.Succeeded, first.Status);
        Assert.Equal(AiActionRequestStatus.Succeeded, second.Status);
        Assert.Equal(first.ExternalExecutionId, second.ExternalExecutionId);
        Assert.Equal(1, executor.ExecuteCalls);
    }

    [Fact]
    public async Task CreateActionAsync_RejectsSourceSystemValidationFailure()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor
        {
            ValidationResponse = new ExternalActionValidationResponse(false, "missing_due_date", null)
        };
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"No due date"}""",
                AiActionApprovalMode.Manual,
                "invalid-action")));

        Assert.Equal("action_validation_failed", exception.Message);
        Assert.Equal(1, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
        Assert.Empty(fixture.DbContext.AiActionRequests);
    }

    private static ActionApprovalService CreateService(
        AppDbContext dbContext,
        Guid tenantId,
        FakeExternalActionExecutor executor,
        CapturingAuditLogService? auditLog = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant");
        return new ActionApprovalService(
            dbContext,
            tenantContext,
            new AllowingExternalAccessResolver(),
            executor,
            new DefaultActionApprovalRuleService(),
            auditLog ?? new CapturingAuditLogService());
    }

    private static async Task<ActionSeed> SeedRecommendationAsync(AppDbContext dbContext)
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
        var workflow = new WorkflowDefinitionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            Name = "Proposal workflow",
            EventType = "deal.stage.changed",
            ObjectType = "deal",
            TriggerStage = "proposal",
            Status = WorkflowDefinitionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var domainEvent = new DomainEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            WorkflowDefinitionId = workflow.Id,
            EventType = "deal.stage.changed",
            ObjectType = "deal",
            ExternalObjectId = "D-100",
            IdempotencyKey = "event-001",
            OccurredAt = now,
            Status = DomainEventStatus.RecommendationCreated,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var recommendation = new AiRecommendationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            DomainEventId = domainEvent.Id,
            WorkflowDefinitionId = workflow.Id,
            ObjectType = "deal",
            ExternalObjectId = "D-100",
            Title = "Proposal next steps",
            Summary = "Follow up with the customer.",
            RecommendedNextStepsJson = """["Create a follow-up task."]""",
            RisksJson = "[]",
            ClarificationQuestionsJson = "[]",
            SuggestedTasksJson = """["Create CRM task."]""",
            WarningsJson = "[]",
            WonLostSignalsJson = """["Reasoning-based, not predictive ML: next step owner is present."]""",
            ReasoningLabel = "Reasoning-based signal, not predictive ML.",
            SourcesJson = "[]",
            Status = AiRecommendationStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var connection = new IntegrationConnectionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            Name = "CRM connector",
            BaseUrl = "https://crm.example.local",
            AuthMode = IntegrationAuthMode.None,
            Status = IntegrationConnectionStatus.Active,
            SecretReference = "crm",
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Applications.Add(application);
        dbContext.Teams.Add(team);
        dbContext.Users.Add(user);
        dbContext.WorkflowDefinitions.Add(workflow);
        dbContext.DomainEvents.Add(domainEvent);
        dbContext.AiRecommendations.Add(recommendation);
        dbContext.IntegrationConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        return new ActionSeed(tenant, application, user, recommendation);
    }

    private sealed class FakeExternalActionExecutor : IExternalActionExecutor
    {
        public int ValidateCalls { get; private set; }

        public int ExecuteCalls { get; private set; }

        public ExternalActionValidationResponse ValidationResponse { get; set; } =
            new(true, null, """{"title":"normalized follow-up","normalized":true}""");

        public ExternalActionExecutionResponse ExecutionResponse { get; set; } =
            new(true, "external-action-001", """{"created":true}""", null);

        public Task<ExternalActionValidationResponse> ValidateActionAsync(
            ExternalConnectorContext context,
            ExternalActionValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateCalls++;
            return Task.FromResult(ValidationResponse);
        }

        public Task<ExternalActionExecutionResponse> ExecuteActionAsync(
            ExternalConnectorContext context,
            ExternalActionExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            return Task.FromResult(ExecutionResponse);
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

    private sealed class CapturingAuditLogService : IAuditLogService
    {
        public List<string> Actions { get; } = [];

        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
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

    private sealed record ActionSeed(
        TenantEntity Tenant,
        ApplicationEntity Application,
        UserEntity User,
        AiRecommendationEntity Recommendation);
}
