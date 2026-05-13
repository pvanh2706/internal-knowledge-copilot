using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
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
        var service = CreateService(dbContext);

        var response = await service.SubmitAsync(
            interactionId,
            userId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Missing log check"));

        Assert.Equal(AiFeedbackValue.Incorrect, response.Value);
        Assert.Equal(FeedbackReviewStatus.New, response.ReviewStatus);
        Assert.Equal("Missing log check", response.Note);
        Assert.Equal(1, await dbContext.AiFeedback.CountAsync());
        Assert.Equal(1, await dbContext.AiQualityIssues.CountAsync());
        Assert.Equal(1, await dbContext.ProcessingJobs.CountAsync(job => job.JobType == "ClassifyAiFailure"));
    }

    [Fact]
    public async Task UpdateReviewStatusAsync_ResolvesIncorrectFeedback()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        await SeedInteractionAsync(dbContext, userId, interactionId);
        var service = CreateService(dbContext);
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

    [Fact]
    public async Task ClassifyIssueAsync_AddsFailureClassification()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        await SeedInteractionAsync(dbContext, userId, interactionId);
        var service = CreateService(dbContext);
        await service.SubmitAsync(
            interactionId,
            userId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Missing retry step"));
        var issue = await dbContext.AiQualityIssues.SingleAsync();

        await service.ClassifyIssueAsync(issue.Id);

        var classified = await dbContext.AiQualityIssues.SingleAsync();
        Assert.Equal(AiQualityIssueStatus.Classified, classified.Status);
        Assert.NotNull(classified.FailureType);
        Assert.Contains("CreateCorrection", classified.RecommendedActionsJson);
    }

    [Fact]
    public async Task ApproveCorrectionAsync_IndexesCorrectionAndResolvesIssue()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await SeedInteractionAsync(dbContext, userId, interactionId, documentId, folderId);
        var vectorStore = new FakeKnowledgeVectorStore();
        var service = CreateService(dbContext, vectorStore);
        await service.SubmitAsync(
            interactionId,
            userId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Wrong answer"));
        var issue = await dbContext.AiQualityIssues.SingleAsync();
        var correction = await service.CreateCorrectionAsync(
            issue.Id,
            reviewerId,
            new CreateCorrectionRequest("Always check provider logs before retry.", VisibilityScope.Folder, folderId, false));

        var approved = await service.ApproveCorrectionAsync(correction.Id, reviewerId);

        Assert.Equal(KnowledgeCorrectionStatus.Approved, approved.Status);
        Assert.Single(vectorStore.UpsertedChunks);
        Assert.Equal("correction", vectorStore.UpsertedChunks[0].Metadata["source_type"]);
        Assert.Equal("approved", vectorStore.UpsertedChunks[0].Metadata["status"]);
        Assert.Equal(1, await dbContext.KnowledgeChunks.CountAsync(chunk => chunk.SourceType == KnowledgeSourceType.Correction));
        Assert.Equal(1, await dbContext.KnowledgeChunkIndexes.CountAsync(chunk => chunk.SourceType == KnowledgeSourceType.Correction));
        Assert.Equal(AiQualityIssueStatus.Resolved, (await dbContext.AiQualityIssues.SingleAsync()).Status);
        Assert.Equal(FeedbackReviewStatus.Resolved, (await dbContext.AiFeedback.SingleAsync()).ReviewStatus);
    }

    private static async Task SeedInteractionAsync(AppDbContext dbContext, Guid userId, Guid interactionId, Guid? documentId = null, Guid? folderId = null)
    {
        var now = DateTimeOffset.UtcNow;
        folderId ??= Guid.NewGuid();
        documentId ??= Guid.NewGuid();
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
        dbContext.Folders.Add(new FolderEntity
        {
            Id = folderId.Value,
            Name = "Support",
            Path = "/Support",
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId.Value,
            FolderId = folderId.Value,
            Title = "Support Guide",
            Status = DocumentStatus.Approved,
            CreatedByUserId = userId,
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
            Sources =
            [
                new AiInteractionSourceEntity
                {
                    Id = Guid.NewGuid(),
                    SourceType = KnowledgeSourceType.Document,
                    SourceId = Guid.NewGuid().ToString(),
                    DocumentId = documentId,
                    Title = "Support Guide",
                    FolderPath = "/Support",
                    Excerpt = "Original answer source.",
                    Rank = 1,
                    CreatedAt = now,
                },
            ],
        });
        await dbContext.SaveChangesAsync();
    }

    private static AiFeedbackService CreateService(AppDbContext dbContext, FakeKnowledgeVectorStore? vectorStore = null)
    {
        return new AiFeedbackService(
            dbContext,
            new NoopAuditLogService(),
            new MockEmbeddingService(),
            vectorStore ?? new FakeKnowledgeVectorStore(),
            new KnowledgeChunkLedgerService(dbContext),
            new KnowledgeKeywordIndexService(dbContext));
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

    private sealed class FakeKnowledgeVectorStore : IKnowledgeVectorStore
    {
        public List<KnowledgeChunkRecord> UpsertedChunks { get; } = [];

        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default)
        {
            UpsertedChunks.AddRange(chunks);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(
            float[] embedding,
            int limit,
            KnowledgeQueryFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeVectorSearchResult>>([]);
        }
    }
}
