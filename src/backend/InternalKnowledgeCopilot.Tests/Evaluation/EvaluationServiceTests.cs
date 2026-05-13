using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.Evaluation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Evaluation;

public sealed class EvaluationServiceTests
{
    [Fact]
    public async Task CreateCaseFromFeedbackAsync_CopiesQuestionAndScope()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var feedbackId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await SeedFeedbackAsync(dbContext, userId, reviewerId, feedbackId, interactionId, AiScopeType.Folder, folderId, null);
        var service = CreateService(dbContext, new FakeAiQuestionService("unused"));

        var response = await service.CreateCaseFromFeedbackAsync(
            feedbackId,
            reviewerId,
            new CreateEvaluationCaseFromFeedbackRequest(
                "Check provider logs before retry.",
                ["provider logs", "retry"],
                null,
                null,
                null));

        Assert.Equal(feedbackId, response.SourceFeedbackId);
        Assert.Equal("How should support handle payment errors?", response.Question);
        Assert.Equal(AiScopeType.Folder, response.ScopeType);
        Assert.Equal(folderId, response.FolderId);
        Assert.Contains("provider logs", response.ExpectedKeywords);
        Assert.Equal(1, await dbContext.EvaluationCases.CountAsync());
    }

    [Fact]
    public async Task RunAsync_PassesWhenAnswerContainsExpectedKeywords()
    {
        await using var dbContext = CreateDbContext();
        var reviewerId = Guid.NewGuid();
        await SeedReviewerAsync(dbContext, reviewerId);
        var evaluationCaseId = await SeedEvaluationCaseAsync(
            dbContext,
            reviewerId,
            """["provider logs","retry"]""");
        var aiQuestionService = new FakeAiQuestionService("Support must check provider logs before retry.");
        var service = CreateService(dbContext, aiQuestionService);

        var response = await service.RunAsync(reviewerId, new RunEvaluationRequest(null, "baseline"));

        Assert.Equal(1, response.TotalCases);
        Assert.Equal(1, response.PassedCases);
        Assert.Equal(100, response.PassRate);
        Assert.Equal(evaluationCaseId, response.Results.Single().EvaluationCaseId);
        Assert.True(response.Results.Single().Passed);
        Assert.Single(aiQuestionService.Requests);
        Assert.Equal(1, await dbContext.EvaluationRunResults.CountAsync(result => result.Passed));
    }

    [Fact]
    public async Task RunAsync_FailsWhenExpectedKeywordIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var reviewerId = Guid.NewGuid();
        await SeedReviewerAsync(dbContext, reviewerId);
        await SeedEvaluationCaseAsync(
            dbContext,
            reviewerId,
            """["provider logs","retry"]""");
        var service = CreateService(dbContext, new FakeAiQuestionService("Support can retry the payment."));

        var response = await service.RunAsync(reviewerId, new RunEvaluationRequest(null, "after-change"));

        Assert.Equal(0, response.PassedCases);
        Assert.Equal(1, response.FailedCases);
        Assert.Contains("provider logs", response.Results.Single().FailureReason);
    }

    private static async Task SeedFeedbackAsync(
        AppDbContext dbContext,
        Guid userId,
        Guid reviewerId,
        Guid feedbackId,
        Guid interactionId,
        AiScopeType scopeType,
        Guid? folderId,
        Guid? documentId)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.Users.AddRange(
            new UserEntity
            {
                Id = userId,
                Email = "user@example.local",
                DisplayName = "User",
                PasswordHash = "hash",
                Role = UserRole.User,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new UserEntity
            {
                Id = reviewerId,
                Email = "reviewer@example.local",
                DisplayName = "Reviewer",
                PasswordHash = "hash",
                Role = UserRole.Reviewer,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        dbContext.AiInteractions.Add(new AiInteractionEntity
        {
            Id = interactionId,
            UserId = userId,
            Question = "How should support handle payment errors?",
            Answer = "Old incomplete answer.",
            ScopeType = scopeType,
            ScopeFolderId = folderId,
            ScopeDocumentId = documentId,
            CreatedAt = now,
        });
        dbContext.AiFeedback.Add(new AiFeedbackEntity
        {
            Id = feedbackId,
            AiInteractionId = interactionId,
            UserId = userId,
            Value = AiFeedbackValue.Incorrect,
            Note = "Missing provider log step.",
            ReviewStatus = FeedbackReviewStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedReviewerAsync(AppDbContext dbContext, Guid reviewerId)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new UserEntity
        {
            Id = reviewerId,
            Email = $"{reviewerId:N}@example.local",
            DisplayName = "Reviewer",
            PasswordHash = "hash",
            Role = UserRole.Reviewer,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedEvaluationCaseAsync(AppDbContext dbContext, Guid reviewerId, string expectedKeywordsJson)
    {
        var now = DateTimeOffset.UtcNow;
        var evaluationCaseId = Guid.NewGuid();
        dbContext.EvaluationCases.Add(new EvaluationCaseEntity
        {
            Id = evaluationCaseId,
            Question = "How should support handle payment errors?",
            ExpectedAnswer = "Check provider logs before retry.",
            ExpectedKeywordsJson = expectedKeywordsJson,
            ScopeType = AiScopeType.All,
            CreatedByUserId = reviewerId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
        return evaluationCaseId;
    }

    private static EvaluationService CreateService(AppDbContext dbContext, IAiQuestionService aiQuestionService)
    {
        return new EvaluationService(dbContext, aiQuestionService, new NoopAuditLogService());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeAiQuestionService(string answer, bool needsClarification = false) : IAiQuestionService
    {
        public List<AskQuestionRequest> Requests { get; } = [];

        public Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new AskQuestionResponse(
                Guid.NewGuid(),
                answer,
                needsClarification,
                "high",
                [],
                [],
                [],
                []));
        }
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
