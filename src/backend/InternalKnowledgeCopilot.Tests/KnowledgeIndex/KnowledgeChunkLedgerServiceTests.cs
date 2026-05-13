using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.KnowledgeIndex;

public sealed class KnowledgeChunkLedgerServiceTests
{
    [Fact]
    public async Task ReplaceChunksAsync_StoresFullChunkMetadataForRebuild()
    {
        await using var dbContext = CreateDbContext();
        var service = new KnowledgeChunkLedgerService(dbContext);
        var sourceId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        await service.ReplaceChunksAsync(
            KnowledgeSourceType.Document,
            sourceId,
            [
                new KnowledgeChunkRecord(
                    "chunk-1",
                    [0.1f, 0.2f],
                    "Payment retry must check provider logs.",
                    new Dictionary<string, object>
                    {
                        ["source_type"] = "document",
                        ["source_id"] = sourceId,
                        ["document_id"] = documentId.ToString(),
                        ["document_version_id"] = versionId.ToString(),
                        ["folder_id"] = folderId.ToString(),
                        ["visibility_scope"] = "folder",
                        ["status"] = "approved",
                        ["title"] = "Payment Guide",
                        ["folder_path"] = "/Payments",
                        ["section_title"] = "Retry",
                        ["section_index"] = 2,
                        ["chunk_index"] = 3,
                        ["keywords"] = "payment, retry",
                    }),
            ]);
        await dbContext.SaveChangesAsync();

        var row = await dbContext.KnowledgeChunks.SingleAsync();
        Assert.Equal("chunk-1", row.ChunkId);
        Assert.Equal(sourceId, row.SourceId);
        Assert.Equal(documentId, row.DocumentId);
        Assert.Equal(versionId, row.DocumentVersionId);
        Assert.Equal(folderId, row.FolderId);
        Assert.Equal("Retry", row.SectionTitle);
        Assert.Equal(3, row.ChunkIndex);
        Assert.Equal("chunk-1", row.VectorId);
        Assert.False(string.IsNullOrWhiteSpace(row.TextHash));
        Assert.Contains("payment, retry", row.MetadataJson);

        var snapshots = await service.GetChunksForSourceAsync(KnowledgeSourceType.Document, sourceId);
        var snapshot = Assert.Single(snapshots);
        Assert.Equal("Payment Guide", snapshot.Title);
        Assert.Equal(row.TextHash, snapshot.TextHash);
    }

    [Fact]
    public async Task ReplaceChunksAsync_RemovesStaleChunksForSameSource()
    {
        await using var dbContext = CreateDbContext();
        var service = new KnowledgeChunkLedgerService(dbContext);
        var sourceId = Guid.NewGuid().ToString();

        await service.ReplaceChunksAsync(KnowledgeSourceType.Wiki, sourceId, [CreateChunk("old", sourceId, 0)]);
        await dbContext.SaveChangesAsync();

        await service.ReplaceChunksAsync(KnowledgeSourceType.Wiki, sourceId, [CreateChunk("new", sourceId, 0)]);
        await dbContext.SaveChangesAsync();

        var row = await dbContext.KnowledgeChunks.SingleAsync();
        Assert.Equal("new", row.ChunkId);
    }

    private static KnowledgeChunkRecord CreateChunk(string id, string sourceId, int index)
    {
        return new KnowledgeChunkRecord(
            id,
            [1f],
            $"Wiki content {id}",
            new Dictionary<string, object>
            {
                ["source_type"] = "wiki",
                ["source_id"] = sourceId,
                ["wiki_page_id"] = sourceId,
                ["visibility_scope"] = "company",
                ["status"] = "published",
                ["title"] = "Wiki",
                ["folder_path"] = string.Empty,
                ["chunk_index"] = index,
            });
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
