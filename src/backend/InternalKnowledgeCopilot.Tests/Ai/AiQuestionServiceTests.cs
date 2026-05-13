using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
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
            new KnowledgeKeywordIndexService(dbContext),
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
            new KnowledgeKeywordIndexService(dbContext),
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
            new KnowledgeKeywordIndexService(dbContext),
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
            new KnowledgeKeywordIndexService(dbContext),
            new MockAnswerGenerationService());

        var response = await service.AskAsync(userId, new AskQuestionRequest("payment error", AiScopeType.All, null, null));

        Assert.NotEmpty(response.Citations);
        Assert.Equal(KnowledgeSourceType.Correction, response.Citations[0].SourceType);
        Assert.Contains("provider logs", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_ReranksExactKeywordMatchAheadOfCloserVectorDistance()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedVisibleKnowledgeAsync(dbContext, documentCount: 2);

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateVectorResult(
                "generic",
                seed.FolderId,
                seed.DocumentIds[0],
                seed.VersionIds[0],
                "Generic payment source",
                "/Allowed",
                "payment timeout can be retried later",
                distance: 0.01),
            CreateVectorResult(
                "exact",
                seed.FolderId,
                seed.DocumentIds[1],
                seed.VersionIds[1],
                "Provider log source",
                "/Allowed",
                "payment timeout requires provider logs and chargeback ticket escalation",
                distance: 0.95),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeKeywordIndexService(dbContext),
            new MockAnswerGenerationService());

        var response = await service.AskAsync(seed.UserId, new AskQuestionRequest("provider logs chargeback", AiScopeType.All, null, null));

        Assert.Equal("Provider log source", response.Citations[0].Title);
        Assert.Contains("provider logs", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_UsesKeywordIndexWhenVectorSearchHasNoResults()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedVisibleKnowledgeAsync(dbContext, documentCount: 1);
        var keywordIndexService = new KnowledgeKeywordIndexService(dbContext);
        var keywordChunk = CreateKeywordChunk(
            "keyword-only",
            seed.FolderId,
            seed.DocumentIds[0],
            seed.VersionIds[0],
            "Chargeback keyword source",
            "/Allowed",
            "chargeback evidence must include provider logs and customer confirmation");
        await keywordIndexService.ReplaceChunksAsync(KnowledgeSourceType.Document, seed.VersionIds[0].ToString(), [keywordChunk]);
        await dbContext.SaveChangesAsync();

        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            new FakeKnowledgeVectorStore([]),
            keywordIndexService,
            new MockAnswerGenerationService());

        var response = await service.AskAsync(seed.UserId, new AskQuestionRequest("chargeback provider logs", AiScopeType.All, null, null));

        var citation = Assert.Single(response.Citations);
        Assert.Equal("Chargeback keyword source", citation.Title);
        Assert.Contains("customer confirmation", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainRetrievalAsync_ReturnsDiagnosticsWithoutSavingInteraction()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedVisibleKnowledgeAsync(dbContext, documentCount: 2);

        var vectorStore = new FakeKnowledgeVectorStore([
            CreateVectorResult(
                "generic",
                seed.FolderId,
                seed.DocumentIds[0],
                seed.VersionIds[0],
                "Generic payment source",
                "/Allowed",
                "payment timeout can be retried later",
                distance: 0.01),
            CreateVectorResult(
                "exact",
                seed.FolderId,
                seed.DocumentIds[1],
                seed.VersionIds[1],
                "Provider log source",
                "/Allowed",
                "payment timeout requires provider logs and chargeback ticket escalation",
                distance: 0.95),
        ]);
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeKeywordIndexService(dbContext),
            new MockAnswerGenerationService());

        var response = await service.ExplainRetrievalAsync(
            seed.UserId,
            new AskQuestionRequest("provider logs chargeback", AiScopeType.All, null, null));

        Assert.Equal(["provider", "logs", "chargeback"], response.QueryUnderstanding.Keywords);
        Assert.Equal(2, response.CandidateStats.VectorCandidateCount);
        Assert.Equal(2, response.CandidateStats.MergedCandidateCount);
        Assert.Equal(2, response.CandidateStats.AllowedCandidateCount);
        Assert.Equal("Provider log source", response.FinalContext[0].Title);
        Assert.True(response.FinalContext[0].SelectedForContext);
        Assert.Contains("chargeback", response.FinalContext[0].MatchedKeywords);
        Assert.Contains(response.Candidates, candidate => candidate.Decision.Contains("Selected for final context", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await dbContext.AiInteractions.CountAsync());
    }

    [Fact]
    public async Task AskAsync_PacksAtMostEightChunksAndThreePerDocument()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedVisibleKnowledgeAsync(dbContext, documentCount: 3);
        var vectorResults = new List<KnowledgeVectorSearchResult>();

        for (var documentIndex = 0; documentIndex < seed.DocumentIds.Length; documentIndex++)
        {
            for (var sectionIndex = 1; sectionIndex <= 4; sectionIndex++)
            {
                vectorResults.Add(CreateVectorResult(
                    $"doc-{documentIndex}-section-{sectionIndex}",
                    seed.FolderId,
                    seed.DocumentIds[documentIndex],
                    seed.VersionIds[documentIndex],
                    $"Audit source {documentIndex}",
                    "/Allowed",
                    $"payment audit evidence document {documentIndex} section {sectionIndex}",
                    distance: 0.1 + (documentIndex * 0.01) + (sectionIndex * 0.001),
                    sectionIndex: sectionIndex));
            }
        }

        var answerService = new CapturingAnswerGenerationService();
        var service = new AiQuestionService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockEmbeddingService(),
            new FakeKnowledgeVectorStore(vectorResults),
            new KnowledgeKeywordIndexService(dbContext),
            answerService);

        await service.AskAsync(seed.UserId, new AskQuestionRequest("payment audit evidence", AiScopeType.All, null, null));

        Assert.NotNull(answerService.CapturedChunks);
        Assert.Equal(8, answerService.CapturedChunks.Count);
        Assert.All(
            answerService.CapturedChunks.GroupBy(chunk => chunk.DocumentId),
            group => Assert.True(group.Count() <= 3));
    }

    private static KnowledgeChunkRecord CreateKeywordChunk(string id, Guid folderId, Guid documentId, Guid versionId, string title, string folderPath, string text)
    {
        return new KnowledgeChunkRecord(
            id,
            [1f, 0f, 0f],
            text,
            new Dictionary<string, object>
            {
                ["chunk_id"] = id,
                ["source_type"] = "document",
                ["source_id"] = versionId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["document_version_id"] = versionId.ToString(),
                ["folder_id"] = folderId.ToString(),
                ["title"] = title,
                ["folder_path"] = folderPath,
                ["section_title"] = "Evidence",
                ["section_index"] = 1,
                ["status"] = "approved",
                ["visibility_scope"] = "folder",
            });
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

    private static async Task<VisibleKnowledgeSeed> SeedVisibleKnowledgeAsync(AppDbContext dbContext, int documentCount)
    {
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var documentIds = Enumerable.Range(0, documentCount).Select(_ => Guid.NewGuid()).ToArray();
        var versionIds = Enumerable.Range(0, documentCount).Select(_ => Guid.NewGuid()).ToArray();

        dbContext.Teams.Add(new TeamEntity { Id = teamId, Name = $"Team {teamId:N}", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"{userId:N}@example.local",
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
            Path = $"/Allowed-{folderId:N}",
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

        for (var index = 0; index < documentCount; index++)
        {
            dbContext.Documents.Add(new DocumentEntity
            {
                Id = documentIds[index],
                FolderId = folderId,
                Title = $"Knowledge source {index}",
                Status = DocumentStatus.Approved,
                CurrentVersionId = versionIds[index],
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            });
            dbContext.DocumentVersions.Add(CreateIndexedVersion(versionIds[index], documentIds[index], userId, now));
        }

        await dbContext.SaveChangesAsync();
        return new VisibleKnowledgeSeed(userId, folderId, documentIds, versionIds);
    }

    private static KnowledgeVectorSearchResult CreateVectorResult(
        string id,
        Guid folderId,
        Guid documentId,
        Guid versionId,
        string title,
        string folderPath,
        string text,
        double distance = 0.1,
        int sectionIndex = 1)
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
                ["section_index"] = sectionIndex,
            },
            distance);
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

        public Task ResetCollectionAsync(CancellationToken cancellationToken = default)
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

    private sealed class CapturingAnswerGenerationService : IAnswerGenerationService
    {
        public IReadOnlyList<RetrievedKnowledgeChunk> CapturedChunks { get; private set; } = [];

        public Task<AiAnswerDraft> GenerateAsync(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        {
            CapturedChunks = chunks.ToList();
            return Task.FromResult(new AiAnswerDraft(
                "Captured chunks.",
                false,
                "high",
                [],
                [],
                [],
                chunks.Select(chunk => chunk.SourceId).ToArray()));
        }
    }

    private sealed record VisibleKnowledgeSeed(Guid UserId, Guid FolderId, Guid[] DocumentIds, Guid[] VersionIds);
}
