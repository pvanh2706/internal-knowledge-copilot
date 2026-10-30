using System.Text.Json;
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
using static InternalKnowledgeCopilot.Tests.ActionApprovals.ActionRequestTestData;

namespace InternalKnowledgeCopilot.Tests.ActionApprovals;

// S01-E0 Read-only Containment: create/approve/execute không được gọi ra hệ thống nguồn và không
// được ghi đè lịch sử action; các thao tác review cục bộ vẫn hoạt động.
public sealed class ActionApprovalServiceTests
{
    [Fact]
    public async Task ApprovalLifecycle_IsBlockedWithoutSourceCallsOrStateChanges()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var auditLog = new CapturingAuditLogService();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor, auditLog);

        var createError = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"Follow up with customer","dueDate":"2026-06-10"}""",
                AiActionApprovalMode.Manual,
                "action-001")));
        var pending = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.PendingApproval);
        var approveError = await Assert.ThrowsAsync<ExternalActionBlockedException>(() =>
            service.ApproveActionAsync(pending.Id, seed.User.Id, new ApproveAiActionRequest("Looks safe.")));
        var executeError = await Assert.ThrowsAsync<ExternalActionBlockedException>(() =>
            service.ExecuteActionAsync(pending.Id, seed.User.Id));

        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, createError.Message);
        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, approveError.Message);
        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, executeError.Message);
        Assert.Equal(0, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
        var stored = await fixture.DbContext.AiActionRequests.AsNoTracking().SingleAsync();
        AssertHistoryUnchanged(pending, stored);
        Assert.Equal(
            new[] { "create", "approve", "execute" },
            auditLog.Entries
                .Where(entry => entry.Action == "AiActionRequestBlocked")
                .Select(entry => JsonDocument.Parse(entry.MetadataJson!).RootElement.GetProperty("Operation").GetString()));
        Assert.DoesNotContain(auditLog.Entries, entry => entry.Action is
            "AiActionRequestCreated" or
            "AiActionRequestApproved" or
            "AiActionRequestExecuting" or
            "AiActionRequestSucceeded" or
            "AiActionRequestFailed");
    }

    [Fact]
    public async Task CreateActionAsync_DoesNotAutoApproveSafeRuleTask()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var auditLog = new CapturingAuditLogService();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor, auditLog);

        var error = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"Schedule follow-up"}""",
                AiActionApprovalMode.Rule,
                "rule-action-001")));

        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, error.Message);
        Assert.Empty(fixture.DbContext.AiActionRequests);
        Assert.DoesNotContain(auditLog.Entries, entry => entry.Action == "AiActionRequestApprovedByRule");
        Assert.Equal(0, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
    }

    [Theory]
    [InlineData("create_task")]
    [InlineData("create_follow_up_task")]
    public async Task DefaultActionApprovalRuleService_NeverAutoApprovesInReadOnlyMode(string actionType)
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);

        var decision = await new DefaultActionApprovalRuleService().EvaluateAsync(
            seed.Recommendation,
            actionType,
            seed.Recommendation.ObjectType,
            seed.Recommendation.ExternalObjectId,
            """{"title":"Schedule follow-up"}""");

        Assert.False(decision.IsApproved);
        Assert.Equal(ExternalActionReadOnlyPolicy.RuleAutoApprovalDisabledReason, decision.Reason);
    }

    [Fact]
    public async Task ExecuteActionAsync_KeepsHistoricalSucceededActionUnchanged()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);
        var executedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var succeeded = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.Succeeded, action =>
        {
            action.ApprovedByUserId = seed.User.Id;
            action.ExecutedByUserId = seed.User.Id;
            action.ExternalExecutionId = "external-action-001";
            action.ExecutionResultJson = """{"created":true}""";
            action.ExecutingStartedAt = executedAt.AddSeconds(-1);
            action.ExecutedAt = executedAt;
        });

        var first = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.ExecuteActionAsync(succeeded.Id, seed.User.Id));
        var second = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.ExecuteActionAsync(succeeded.Id, seed.User.Id));

        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, first.Message);
        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, second.Message);
        Assert.Equal(0, executor.ExecuteCalls);
        Assert.Equal(0, executor.ValidateCalls);
        var stored = await fixture.DbContext.AiActionRequests.AsNoTracking().SingleAsync();
        AssertHistoryUnchanged(succeeded, stored);
    }

    [Fact]
    public async Task CreateActionAsync_DoesNotInvokeSourceValidation()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext);
        var executor = new FakeExternalActionExecutor
        {
            ValidationResponse = new ExternalActionValidationResponse(true, null, """{"title":"accepted by source"}""")
        };
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.CreateActionAsync(
            seed.Recommendation.Id,
            seed.User.Id,
            new CreateAiActionRequest(
                "create_task",
                null,
                null,
                """{"title":"No due date"}""",
                AiActionApprovalMode.Manual,
                "invalid-action")));

        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, exception.Message);
        Assert.Equal(0, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
        Assert.Empty(fixture.DbContext.AiActionRequests);
    }

    [Theory]
    [InlineData(AiActionRequestStatus.Approved, UserRole.Admin)]
    [InlineData(AiActionRequestStatus.Approved, UserRole.Reviewer)]
    [InlineData(AiActionRequestStatus.Approved, UserRole.User)]
    [InlineData(AiActionRequestStatus.Failed, UserRole.Admin)]
    [InlineData(AiActionRequestStatus.Executing, UserRole.Admin)]
    public async Task ExecuteActionAsync_BlocksLegacyActionsForEveryRole(AiActionRequestStatus status, UserRole role)
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext, role);
        var executor = new FakeExternalActionExecutor();
        var auditLog = new CapturingAuditLogService();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor, auditLog);
        var action = await SeedActionAsync(fixture.DbContext, seed, status, item =>
        {
            item.ApprovedByUserId = seed.User.Id;
            item.ApprovedAt = item.CreatedAt.AddMinutes(5);
            item.ExecutionError = status == AiActionRequestStatus.Failed ? "source_timeout" : null;
        });

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.ExecuteActionAsync(action.Id, seed.User.Id));

        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, exception.Message);
        Assert.Equal(0, executor.ExecuteCalls);
        Assert.Equal(0, executor.ValidateCalls);
        var stored = await fixture.DbContext.AiActionRequests.AsNoTracking().SingleAsync();
        AssertHistoryUnchanged(action, stored);
        var audit = Assert.Single(auditLog.Entries);
        Assert.Equal("AiActionRequestBlocked", audit.Action);
        Assert.Equal("AiActionRequest", audit.EntityType);
        Assert.Equal(action.Id, audit.EntityId);
        Assert.Equal(seed.User.Id, audit.ActorUserId);
        Assert.Contains(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, audit.MetadataJson);
        Assert.DoesNotContain("Legacy follow-up", audit.MetadataJson);
    }

    [Fact]
    public async Task ExecuteActionAsync_BlocksDirectCallThroughRealConnectorWithoutOutboundRequest()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext, UserRole.Admin);
        var handler = new RecordingHttpMessageHandler();
        var connector = new InternalHttpExternalConnector(new HttpClient(handler));
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, connector);
        var action = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.Approved);

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => service.ExecuteActionAsync(action.Id, seed.User.Id));

        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, exception.Message);
        Assert.Empty(handler.Requests);
        AssertHistoryUnchanged(action, await fixture.DbContext.AiActionRequests.AsNoTracking().SingleAsync());
    }

    [Fact]
    public async Task ApproveActionAsync_DoesNotInvokeSourceValidationOrRewriteHistory()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext, UserRole.Reviewer);
        var executor = new FakeExternalActionExecutor();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);
        var failed = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.Failed, item =>
        {
            item.ExecutionError = "source_timeout";
            item.ExecutedAt = item.CreatedAt.AddMinutes(10);
        });

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() =>
            service.ApproveActionAsync(failed.Id, seed.User.Id, new ApproveAiActionRequest("Retry it.")));

        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, exception.Message);
        Assert.Equal(0, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
        AssertHistoryUnchanged(failed, await fixture.DbContext.AiActionRequests.AsNoTracking().SingleAsync());
    }

    [Fact]
    public async Task LocalReviewOperations_ContinueWithoutSourceCalls()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seed = await SeedRecommendationAsync(fixture.DbContext, UserRole.Reviewer);
        var executor = new FakeExternalActionExecutor();
        var service = CreateService(fixture.DbContext, seed.Tenant.Id, executor);
        var pending = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.PendingApproval);
        var draft = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.Draft);
        var succeeded = await SeedActionAsync(fixture.DbContext, seed, AiActionRequestStatus.Succeeded, item =>
        {
            item.ExternalExecutionId = "external-action-001";
        });

        var rejected = await service.RejectActionAsync(pending.Id, seed.User.Id, new RejectAiActionRequest("Not needed."));
        var cancelled = await service.CancelActionAsync(draft.Id, seed.User.Id, new CancelAiActionRequest("Duplicate."));
        var listed = await service.GetActionsAsync(seed.User.Id, null, null, null, null, null);

        Assert.Equal(AiActionRequestStatus.Rejected, rejected.Status);
        Assert.Equal(AiActionRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal(3, listed.Count);
        Assert.Contains(listed, item =>
            item.Id == succeeded.Id &&
            item.Status == AiActionRequestStatus.Succeeded &&
            item.ExternalExecutionId == "external-action-001");
        Assert.Equal(0, executor.ValidateCalls);
        Assert.Equal(0, executor.ExecuteCalls);
    }

    private static void AssertHistoryUnchanged(AiActionRequestEntity expected, AiActionRequestEntity actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.ValidationResultJson, actual.ValidationResultJson);
        Assert.Equal(expected.NormalizedPayloadJson, actual.NormalizedPayloadJson);
        Assert.Equal(expected.ApprovedByUserId, actual.ApprovedByUserId);
        Assert.Equal(expected.ApprovedAt, actual.ApprovedAt);
        Assert.Equal(expected.ExecutedByUserId, actual.ExecutedByUserId);
        Assert.Equal(expected.ExecutingStartedAt, actual.ExecutingStartedAt);
        Assert.Equal(expected.ExecutedAt, actual.ExecutedAt);
        Assert.Equal(expected.ExternalExecutionId, actual.ExternalExecutionId);
        Assert.Equal(expected.ExecutionResultJson, actual.ExecutionResultJson);
        Assert.Equal(expected.ExecutionError, actual.ExecutionError);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
    }

    private static ActionApprovalService CreateService(
        AppDbContext dbContext,
        Guid tenantId,
        IExternalActionExecutor executor,
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
        public List<AuditEntry> Entries { get; } = [];

        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            Entries.Add(new AuditEntry(actorUserId, action, entityType, entityId, metadata is null ? null : JsonSerializer.Serialize(metadata)));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(Guid? ActorUserId, string Action, string EntityType, Guid? EntityId, string? MetadataJson);

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
}
