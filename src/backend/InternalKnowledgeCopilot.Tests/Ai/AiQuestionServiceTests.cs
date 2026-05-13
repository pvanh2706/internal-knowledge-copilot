using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class AiQuestionServiceTests
{
    [Fact]
    public async Task AskAsync_OnlyUsesChunksFromVisibleFolders()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedFolderId = Guid.NewGuid();
        var deniedFolderId = Guid.NewGuid();
        var allowedDocumentId = Guid.NewGuid();
        var allowedVersionId = Guid.NewGuid();
        var deniedDocumentId = Guid.NewGuid();
        var deniedVersionId = Guid.NewGuid();

        dbContext.Teams.Add(new TeamEntity { Id = teamId, Name = "Team", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = teamId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.AddRange(
            new FolderEntity { Id = allowedFolderId, Name = "Allowed", Path = "/Allowed", CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now },
            new FolderEntity { Id = deniedFolderId, Name = "Denied", Path = "/Denied", CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now });
        dbContext.FolderPermissions.Add(new FolderPermissionEntity
        {
            Id = Guid.NewGuid(),
            FolderId = allowedFolderId,
            TeamId = teamId,
            CanView = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Documents.AddRange(
            new DocumentEntity
            {
                Id = allowedDocumentId,
                FolderId = allowedFolderId,
                Title = "Allowed payment",
                Status = DocumentStatus.Approved,
                CurrentVersionId = allowedVersionId,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new DocumentEntity
            {
                Id = deniedDocumentId,
                FolderId = deniedFolderId,
                Title = "Denied payment",
                Status = DocumentStatus.Approved,
                CurrentVersionId = deniedVersionId,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        dbContext.DocumentVersions.AddRange(
            CreateIndexedVersion(allowedVersionId, allowedDocumentId, userId, now),
            CreateIndexedVersion(deniedVersionId, deniedDocumentId, userId, now));
        await dbContext.SaveChangesAsync();

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateVectorResult("allowed", allowedFolderId, allowedDocumentId, allowedVersionId, "Allowed payment", "/Allowed", "payment error must be checked in approved allowed source"),
            CreateVectorResult("denied", deniedFolderId, deniedDocumentId, deniedVersionId, "Denied payment", "/Denied", "payment error secret denied source"),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new MockAnswerGenerationService());

        var response = await service.AskAsync(userId, new AskQuestionRequest("payment error", AiScopeType.All, null, null));

        Assert.False(response.NeedsClarification);
        Assert.NotNull(vectorStore.LastFilter);
        Assert.Contains(allowedFolderId, vectorStore.LastFilter.FolderIds);
        Assert.DoesNotContain(deniedFolderId, vectorStore.LastFilter.FolderIds);
        Assert.True(vectorStore.LastFilter.IncludeCompanyVisible);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("Allowed payment", citation.Title);
        Assert.Equal("Troubleshooting", citation.SectionTitle);
        Assert.Equal("low", response.Confidence);
        Assert.DoesNotContain("secret denied", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.AiInteractions.CountAsync());
        Assert.Equal(1, await dbContext.AiInteractionSources.CountAsync());
    }

    [Fact]
    public async Task AskAsync_DoesNotUseWikiChunk_WhenSqliteDoesNotConfirmPublishedPage()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var fakeWikiPageId = Guid.NewGuid();
        var sourceDocumentId = Guid.NewGuid();

        dbContext.Teams.Add(new TeamEntity { Id = teamId, Name = "Team", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = teamId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.Add(new FolderEntity
        {
            Id = folderId,
            Name = "Allowed",
            Path = "/Allowed",
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.FolderPermissions.Add(new FolderPermissionEntity
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            TeamId = teamId,
            CanView = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateWikiVectorResult(fakeWikiPageId, folderId, sourceDocumentId, "Fake wiki", "/Allowed", "secret wiki answer from vector only"),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new MockAnswerGenerationService());

        var response = await service.AskAsync(userId, new AskQuestionRequest("secret wiki", AiScopeType.All, null, null));

        Assert.True(response.NeedsClarification);
        Assert.Equal("low", response.Confidence);
        Assert.Empty(response.Citations);
        Assert.DoesNotContain("secret wiki answer", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.AiInteractions.CountAsync());
        Assert.Equal(0, await dbContext.AiInteractionSources.CountAsync());
    }

    [Fact]
    public async Task AskAsync_OnlyReturnsCitationsSelectedByGroundedAnswer()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var firstDocumentId = Guid.NewGuid();
        var firstVersionId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var secondVersionId = Guid.NewGuid();

        dbContext.Teams.Add(new TeamEntity { Id = teamId, Name = "Team", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = teamId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.Add(new FolderEntity { Id = folderId, Name = "Allowed", Path = "/Allowed", CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now });
        dbContext.FolderPermissions.Add(new FolderPermissionEntity
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            TeamId = teamId,
            CanView = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Documents.AddRange(
            new DocumentEntity
            {
                Id = firstDocumentId,
                FolderId = folderId,
                Title = "First source",
                Status = DocumentStatus.Approved,
                CurrentVersionId = firstVersionId,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new DocumentEntity
            {
                Id = secondDocumentId,
                FolderId = folderId,
                Title = "Second source",
                Status = DocumentStatus.Approved,
                CurrentVersionId = secondVersionId,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        dbContext.DocumentVersions.AddRange(
            CreateIndexedVersion(firstVersionId, firstDocumentId, userId, now),
            CreateIndexedVersion(secondVersionId, secondDocumentId, userId, now));
        await dbContext.SaveChangesAsync();

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateVectorResult("first", folderId, firstDocumentId, firstVersionId, "First source", "/Allowed", "payment error first source"),
            CreateVectorResult("second", folderId, secondDocumentId, secondVersionId, "Second source", "/Allowed", "payment error second source"),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new FixedAnswerGenerationService(new AiAnswerDraft(
                "Answer from selected source.",
                false,
                "high",
                [],
                [],
                [],
                [firstVersionId.ToString()])));

        var response = await service.AskAsync(userId, new AskQuestionRequest("payment error", AiScopeType.All, null, null));

        Assert.Equal("high", response.Confidence);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("First source", citation.Title);
        Assert.Equal(1, await dbContext.AiInteractionSources.CountAsync());
    }

    [Fact]
    public async Task AskAsync_PrioritizesApprovedCorrectionChunks()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        var feedbackId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        var issueId = Guid.NewGuid();

        dbContext.Teams.Add(new TeamEntity { Id = teamId, Name = "Team", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = teamId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.Add(new FolderEntity { Id = folderId, Name = "Allowed", Path = "/Allowed", CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now });
        dbContext.FolderPermissions.Add(new FolderPermissionEntity
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            TeamId = teamId,
            CanView = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            FolderId = folderId,
            Title = "Payment source",
            Status = DocumentStatus.Approved,
            CurrentVersionId = versionId,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.DocumentVersions.Add(CreateIndexedVersion(versionId, documentId, userId, now));
        dbContext.AiInteractions.Add(new AiInteractionEntity
        {
            Id = interactionId,
            UserId = userId,
            Question = "payment error",
            Answer = "Old answer",
            ScopeType = AiScopeType.All,
            CreatedAt = now,
        });
        dbContext.AiFeedback.Add(new AiFeedbackEntity
        {
            Id = feedbackId,
            AiInteractionId = interactionId,
            UserId = userId,
            Value = AiFeedbackValue.Incorrect,
            ReviewStatus = FeedbackReviewStatus.Resolved,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.AiQualityIssues.Add(new AiQualityIssueEntity
        {
            Id = issueId,
            AiFeedbackId = feedbackId,
            AiInteractionId = interactionId,
            Status = AiQualityIssueStatus.Resolved,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.KnowledgeCorrections.Add(new KnowledgeCorrectionEntity
        {
            Id = correctionId,
            QualityIssueId = issueId,
            AiFeedbackId = feedbackId,
            AiInteractionId = interactionId,
            Question = "payment error",
            CorrectionText = "payment error must check provider logs before retry",
            VisibilityScope = VisibilityScope.Folder,
            FolderId = folderId,
            DocumentId = documentId,
            Status = KnowledgeCorrectionStatus.Approved,
            CreatedByUserId = userId,
            ApprovedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            ApprovedAt = now,
            IndexedAt = now,
        });
        await dbContext.SaveChangesAsync();

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateCorrectionVectorResult(correctionId, folderId, documentId, "Payment correction", "/Allowed", "payment error must check provider logs before retry"),
            CreateVectorResult("document", folderId, documentId, versionId, "Payment source", "/Allowed", "payment error old source"),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new MockAnswerGenerationService());

        var response = await service.AskAsync(userId, new AskQuestionRequest("payment error", AiScopeType.All, null, null));

        Assert.NotEmpty(response.Citations);
        Assert.Equal(KnowledgeSourceType.Correction, response.Citations[0].SourceType);
        Assert.Contains("provider logs", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static DocumentVersionEntity CreateIndexedVersion(Guid id, Guid documentId, Guid userId, DateTimeOffset now)
    {
        return new DocumentVersionEntity
        {
            Id = id,
            DocumentId = documentId,
            VersionNumber = 1,
            OriginalFileName = "source.txt",
            StoredFilePath = "source.txt",
            FileExtension = ".txt",
            FileSizeBytes = 10,
            Status = DocumentVersionStatus.Indexed,
            UploadedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static KnowledgeVectorSearchResult CreateVectorResult(string id, Guid folderId, Guid documentId, Guid versionId, string title, string folderPath, string text)
    {
        return new KnowledgeVectorSearchResult(
            id,
            text,
            new Dictionary<string, object?>
            {
                ["source_type"] = "document",
                ["source_id"] = versionId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["document_version_id"] = versionId.ToString(),
                ["folder_id"] = folderId.ToString(),
                ["title"] = title,
                ["folder_path"] = folderPath,
                ["section_title"] = "Troubleshooting",
                ["section_index"] = 1,
            },
            0.1);
    }

    private static KnowledgeVectorSearchResult CreateWikiVectorResult(Guid wikiPageId, Guid folderId, Guid documentId, string title, string folderPath, string text)
    {
        return new KnowledgeVectorSearchResult(
            wikiPageId.ToString(),
            text,
            new Dictionary<string, object?>
            {
                ["source_type"] = "wiki",
                ["source_id"] = wikiPageId.ToString(),
                ["wiki_page_id"] = wikiPageId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["folder_id"] = folderId.ToString(),
                ["visibility_scope"] = "folder",
                ["title"] = title,
                ["folder_path"] = folderPath,
            },
            0.1);
    }

    private static KnowledgeVectorSearchResult CreateCorrectionVectorResult(Guid correctionId, Guid folderId, Guid documentId, string title, string folderPath, string text)
    {
        return new KnowledgeVectorSearchResult(
            correctionId.ToString(),
            text,
            new Dictionary<string, object?>
            {
                ["source_type"] = "correction",
                ["source_id"] = correctionId.ToString(),
                ["correction_id"] = correctionId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["folder_id"] = folderId.ToString(),
                ["visibility_scope"] = "folder",
                ["title"] = title,
                ["folder_path"] = folderPath,
                ["status"] = "approved",
            },
            0.01);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeKnowledgeVectorStore(IReadOnlyList<KnowledgeVectorSearchResult> results) : IKnowledgeVectorStore
    {
        public KnowledgeQueryFilter? LastFilter { get; private set; }

        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
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

    private sealed class FixedAnswerGenerationService(AiAnswerDraft draft) : IAnswerGenerationService
    {
        public Task<AiAnswerDraft> GenerateAsync(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(draft);
        }
    }
}
