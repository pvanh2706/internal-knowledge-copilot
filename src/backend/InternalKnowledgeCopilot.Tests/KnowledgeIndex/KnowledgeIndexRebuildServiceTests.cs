using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.KnowledgeIndex;

public sealed class KnowledgeIndexRebuildServiceTests
{
    [Fact]
    public async Task RebuildAsync_ReplaysLedgerChunksToVectorStoreAndKeywordIndex()
    {
        await using var dbContext = CreateDbContext();
        var vectorStore = new FakeKnowledgeVectorStore();
        var service = CreateService(dbContext, vectorStore);
        var documentVersionId = Guid.NewGuid();
        await SeedLedgerChunkAsync(dbContext, KnowledgeSourceType.Document, documentVersionId.ToString(), "chunk-1", "payment retry provider logs");
        await SeedLedgerChunkAsync(dbContext, KnowledgeSourceType.Document, documentVersionId.ToString(), "chunk-2", "payment escalation customer confirmation", chunkIndex: 1);

        var response = await service.RebuildAsync(
            Guid.NewGuid(),
            new RebuildKnowledgeIndexRequest(ResetVectorStore: false, BatchSize: 1));

        Assert.Equal(2, response.TotalLedgerChunks);
        Assert.Equal(2, response.RebuiltChunks);
        Assert.Equal(2, response.BatchCount);
        Assert.Equal(2, vectorStore.UpsertedChunks.Count);
        Assert.Equal(2, await dbContext.KnowledgeChunkIndexes.CountAsync());
        Assert.Contains(vectorStore.UpsertedChunks, chunk => chunk.Metadata["source_id"].ToString() == documentVersionId.ToString());
    }

    [Fact]
    public async Task RebuildAsync_CanResetVectorStoreBeforeReplay()
    {
        await using var dbContext = CreateDbContext();
        var vectorStore = new FakeKnowledgeVectorStore();
        var service = CreateService(dbContext, vectorStore);
        await SeedLedgerChunkAsync(dbContext, KnowledgeSourceType.Wiki, Guid.NewGuid().ToString(), "wiki-1", "wiki content");

        var response = await service.RebuildAsync(
            Guid.NewGuid(),
            new RebuildKnowledgeIndexRequest(ResetVectorStore: true, BatchSize: 50));

        Assert.True(response.ResetVectorStore);
        Assert.Equal(1, vectorStore.ResetCount);
        Assert.Single(vectorStore.UpsertedChunks);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsLedgerAndKeywordCounts()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeKnowledgeVectorStore());
        await SeedLedgerChunkAsync(dbContext, KnowledgeSourceType.Correction, Guid.NewGuid().ToString(), "correction-1", "approved correction");
        dbContext.KnowledgeChunkIndexes.Add(new KnowledgeChunkIndexEntity
        {
            ChunkId = "keyword-1",
            SourceType = KnowledgeSourceType.Correction,
            SourceId = "correction-1",
            VisibilityScope = "company",
            Status = "approved",
            Title = "Correction",
            FolderPath = string.Empty,
            Text = "approved correction",
            NormalizedText = "approved correction",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var summary = await service.GetSummaryAsync();

        Assert.Equal(1, summary.LedgerChunkCount);
        Assert.Equal(1, summary.KeywordIndexChunkCount);
        Assert.Equal("Correction", Assert.Single(summary.LedgerSourceCounts).SourceType);
    }

    private static async Task SeedLedgerChunkAsync(
        AppDbContext dbContext,
        KnowledgeSourceType sourceType,
        string sourceId,
        string chunkId,
        string text,
        int chunkIndex = 0)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.KnowledgeChunks.Add(new KnowledgeChunkEntity
        {
            ChunkId = chunkId,
            SourceType = sourceType,
            SourceId = sourceId,
            VisibilityScope = sourceType == KnowledgeSourceType.Wiki ? "company" : "folder",
            Status = sourceType == KnowledgeSourceType.Wiki ? "published" : "approved",
            Title = $"{sourceType} source",
            FolderPath = "/Knowledge",
            SectionTitle = "Main",
            SectionIndex = 0,
            ChunkIndex = chunkIndex,
            Text = text,
            TextHash = chunkId,
            VectorId = chunkId,
            MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["chunk_id"] = chunkId,
                ["source_type"] = sourceType.ToString().ToLowerInvariant(),
                ["source_id"] = sourceId,
                ["visibility_scope"] = sourceType == KnowledgeSourceType.Wiki ? "company" : "folder",
                ["status"] = sourceType == KnowledgeSourceType.Wiki ? "published" : "approved",
                ["title"] = $"{sourceType} source",
                ["folder_path"] = "/Knowledge",
                ["section_title"] = "Main",
                ["section_index"] = 0,
                ["chunk_index"] = chunkIndex,
            }),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();
    }

    private static KnowledgeIndexRebuildService CreateService(AppDbContext dbContext, FakeKnowledgeVectorStore vectorStore)
    {
        return new KnowledgeIndexRebuildService(
            dbContext,
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeKeywordIndexService(dbContext),
            new NoopAuditLogService());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeKnowledgeVectorStore : IKnowledgeVectorStore
    {
        public int ResetCount { get; private set; }

        public List<KnowledgeChunkRecord> UpsertedChunks { get; } = [];

        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetCollectionAsync(CancellationToken cancellationToken = default)
        {
            ResetCount += 1;
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

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
