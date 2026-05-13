using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Wiki;

public sealed class WikiServiceTests
{
    [Fact]
    public async Task GenerateDraftAsync_CreatesDraftFromIndexedDocument()
    {
        await using var dbContext = CreateDbContext();
        var setup = await SeedIndexedDocumentAsync(dbContext);
        var vectorStore = new FakeKnowledgeVectorStore();
        var service = CreateService(dbContext, vectorStore);

        var draft = await service.GenerateDraftAsync(
            setup.ReviewerId,
            new GenerateWikiDraftRequest(setup.DocumentId, setup.VersionId));

        Assert.Equal(WikiStatus.Draft, draft.Status);
        Assert.Contains("# Payment Workflow", draft.Content);
        Assert.NotEmpty(draft.MissingInformation);
        Assert.Equal(1, await dbContext.WikiDrafts.CountAsync());
    }

    [Fact]
    public async Task GenerateDraftAsync_AttachesRelatedDocumentsFromVectorSearch()
    {
        await using var dbContext = CreateDbContext();
        var setup = await SeedIndexedDocumentAsync(dbContext);
        var related = await SeedRelatedDocumentAsync(dbContext, setup.ReviewerId, setup.FolderId);
        var vectorStore = new FakeKnowledgeVectorStore
        {
            QueryResults =
            [
                new KnowledgeVectorSearchResult(
                    "related",
                    "provider response code troubleshooting",
                    new Dictionary<string, object?>
                    {
                        ["source_type"] = "document",
                        ["document_id"] = related.DocumentId.ToString(),
                        ["document_version_id"] = related.VersionId.ToString(),
                        ["folder_id"] = setup.FolderId.ToString(),
                        ["title"] = "Provider Retry Guide",
                        ["folder_path"] = "/Payments",
                        ["section_title"] = "Retry rules",
                    },
                    0.1),
            ],
        };
        var service = CreateService(dbContext, vectorStore);

        var draft = await service.GenerateDraftAsync(
            setup.ReviewerId,
            new GenerateWikiDraftRequest(setup.DocumentId, setup.VersionId));

        var relatedDocument = Assert.Single(draft.RelatedDocuments);
        Assert.Equal(related.DocumentId, relatedDocument.DocumentId);
        Assert.Contains("similar knowledge", relatedDocument.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Related documents", draft.Content);
        Assert.NotNull(vectorStore.LastFilter);
        Assert.Contains(setup.FolderId, vectorStore.LastFilter.FolderIds);
    }

    [Fact]
    public async Task PublishAsync_CreatesPageAndIndexesWikiChunks()
    {
        await using var dbContext = CreateDbContext();
        var setup = await SeedIndexedDocumentAsync(dbContext);
        var vectorStore = new FakeKnowledgeVectorStore();
        var service = CreateService(dbContext, vectorStore);
        var draft = await service.GenerateDraftAsync(
            setup.ReviewerId,
            new GenerateWikiDraftRequest(setup.DocumentId, setup.VersionId));

        var page = await service.PublishAsync(
            draft.Id,
            setup.ReviewerId,
            new PublishWikiDraftRequest(VisibilityScope.Folder, setup.FolderId, false));

        Assert.Equal(VisibilityScope.Folder, page.VisibilityScope);
        Assert.Equal(1, await dbContext.WikiPages.CountAsync());
        Assert.NotEmpty(vectorStore.UpsertedChunks);
        Assert.Equal("wiki", vectorStore.UpsertedChunks[0].Metadata["source_type"]);
        Assert.True(await dbContext.KnowledgeChunks.AnyAsync(chunk => chunk.SourceType == KnowledgeSourceType.Wiki));
        Assert.True(await dbContext.KnowledgeChunkIndexes.AnyAsync(chunk => chunk.SourceType == KnowledgeSourceType.Wiki));
    }

    private static IWikiService CreateService(AppDbContext dbContext, FakeKnowledgeVectorStore vectorStore)
    {
        return new WikiService(
            dbContext,
            new FolderPermissionService(dbContext),
            new MockWikiDraftGenerationService(),
            new TextChunker(),
            new SectionDetector(),
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeChunkLedgerService(dbContext),
            new KnowledgeKeywordIndexService(dbContext),
            new NoopAuditLogService());
    }

    private static async Task<SeededDocument> SeedIndexedDocumentAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var reviewerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var extractedTextPath = Path.Combine(Path.GetTempPath(), $"ikc-wiki-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(extractedTextPath, "Payment workflow requires checking logs and provider response code before retry.");

        dbContext.Users.Add(new UserEntity
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
        dbContext.Folders.Add(new FolderEntity
        {
            Id = folderId,
            Name = "Payments",
            Path = "/Payments",
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            FolderId = folderId,
            Title = "Payment Workflow",
            Status = DocumentStatus.Approved,
            CurrentVersionId = versionId,
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            VersionNumber = 1,
            OriginalFileName = "payment.txt",
            StoredFilePath = "payment.txt",
            FileExtension = ".txt",
            FileSizeBytes = 10,
            Status = DocumentVersionStatus.Indexed,
            ExtractedTextPath = extractedTextPath,
            UploadedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
        return new SeededDocument(reviewerId, folderId, documentId, versionId);
    }

    private static async Task<RelatedSeededDocument> SeedRelatedDocumentAsync(AppDbContext dbContext, Guid reviewerId, Guid folderId)
    {
        var now = DateTimeOffset.UtcNow;
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            FolderId = folderId,
            Title = "Provider Retry Guide",
            Status = DocumentStatus.Approved,
            CurrentVersionId = versionId,
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            VersionNumber = 1,
            OriginalFileName = "provider-retry.txt",
            StoredFilePath = "provider-retry.txt",
            FileExtension = ".txt",
            FileSizeBytes = 10,
            Status = DocumentVersionStatus.Indexed,
            UploadedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
        return new RelatedSeededDocument(documentId, versionId);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed record SeededDocument(Guid ReviewerId, Guid FolderId, Guid DocumentId, Guid VersionId);

    private sealed record RelatedSeededDocument(Guid DocumentId, Guid VersionId);

    private sealed class FakeKnowledgeVectorStore : IKnowledgeVectorStore
    {
        public List<KnowledgeChunkRecord> UpsertedChunks { get; } = [];

        public IReadOnlyList<KnowledgeVectorSearchResult> QueryResults { get; init; } = [];

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
            UpsertedChunks.AddRange(chunks);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(
            float[] embedding,
            int limit,
            KnowledgeQueryFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(QueryResults);
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
