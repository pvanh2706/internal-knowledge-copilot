using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Tests.ActionApprovals;

internal static class ActionRequestTestData
{
    public static async Task<ActionSeed> SeedRecommendationAsync(AppDbContext dbContext, UserRole role = UserRole.User)
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
            Role = role,
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

    // Tạo action request "lịch sử" như dữ liệu đã có trước S01-E0. Change tracker được clear để
    // entity trả về là snapshot tách rời, so sánh được với dữ liệu đọc lại từ DB.
    public static async Task<AiActionRequestEntity> SeedActionAsync(
        AppDbContext dbContext,
        ActionSeed seed,
        AiActionRequestStatus status,
        Action<AiActionRequestEntity>? configure = null)
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        var action = new AiActionRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = seed.Tenant.Id,
            ApplicationId = seed.Application.Id,
            RecommendationId = seed.Recommendation.Id,
            ActionType = "create_task",
            TargetObjectType = seed.Recommendation.ObjectType,
            TargetExternalObjectId = seed.Recommendation.ExternalObjectId,
            PayloadJson = """{"title":"Legacy follow-up"}""",
            NormalizedPayloadJson = """{"title":"Legacy follow-up"}""",
            ApprovalMode = AiActionApprovalMode.Manual,
            Status = status,
            IdempotencyKey = $"legacy-{Guid.NewGuid():N}",
            RequestedByUserId = seed.User.Id,
            ValidationResultJson = """{"isValid":true}""",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        configure?.Invoke(action);

        dbContext.AiActionRequests.Add(action);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return action;
    }
}

internal sealed record ActionSeed(
    TenantEntity Tenant,
    ApplicationEntity Application,
    UserEntity User,
    AiRecommendationEntity Recommendation);
