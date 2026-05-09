using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Feedback;

public sealed class AiFeedbackServiceTests
{
    [Fact]
    public async Task SubmitAsync_CreatesIncorrectFeedbackInNewStatus()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        await SeedInteractionAsync(dbContext, userId, interactionId);
        var service = new AiFeedbackService(dbContext, new NoopAuditLogService());

        var response = await service.SubmitAsync(
            interactionId,
            userId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Missing log check"));

        Assert.Equal(AiFeedbackValue.Incorrect, response.Value);
        Assert.Equal(FeedbackReviewStatus.New, response.ReviewStatus);
        Assert.Equal("Missing log check", response.Note);
        Assert.Equal(1, await dbContext.AiFeedback.CountAsync());
    }

    [Fact]
    public async Task UpdateReviewStatusAsync_ResolvesIncorrectFeedback()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        await SeedInteractionAsync(dbContext, userId, interactionId);
        var service = new AiFeedbackService(dbContext, new NoopAuditLogService());
        var feedback = await service.SubmitAsync(
            interactionId,
            userId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Wrong answer"));

        var response = await service.UpdateReviewStatusAsync(
            feedback.Id,
            reviewerId,
            new UpdateFeedbackReviewStatusRequest(FeedbackReviewStatus.Resolved, "Updated source document"));

        Assert.Equal(FeedbackReviewStatus.Resolved, response.ReviewStatus);
        Assert.Equal("Updated source document", response.ReviewerNote);
        var entity = await dbContext.AiFeedback.FirstAsync(item => item.Id == feedback.Id);
        Assert.Equal(reviewerId, entity.ReviewedByUserId);
        Assert.NotNull(entity.ResolvedAt);
    }

    private static async Task SeedInteractionAsync(AppDbContext dbContext, Guid userId, Guid interactionId)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.AiInteractions.Add(new AiInteractionEntity
        {
            Id = interactionId,
            UserId = userId,
            Question = "Question",
            Answer = "Answer",
            ScopeType = AiScopeType.All,
            CreatedAt = now,
        });
        await dbContext.SaveChangesAsync();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
