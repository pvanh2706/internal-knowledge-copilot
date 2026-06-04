using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.KnowledgeIndex;

public sealed class KnowledgeKeywordIndexServiceTests
{
    [Fact]
    public async Task SearchAsync_FiltersByTenantAndVisibleFolders()
    {
        await using var dbContext = CreateDbContext();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var folderAId = Guid.NewGuid();
        var folderBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.KnowledgeChunkIndexes.AddRange(
            CreateIndexChunk("tenant-a", tenantAId, folderAId, "Tenant A policy", "payment escalation tenant a", now),
            CreateIndexChunk("tenant-b", tenantBId, folderBId, "Tenant B secret", "payment escalation tenant b secret", now));
        await dbContext.SaveChangesAsync();

        var service = new KnowledgeKeywordIndexService(dbContext);
        var results = await service.SearchAsync(
            ["payment", "escalation"],
            10,
            new KnowledgeQueryFilter
            {
                TenantId = tenantAId,
                FolderIds = [folderAId],
                IncludeCompanyVisible = true,
                SourceTypes = ["document"],
                Statuses = ["approved"],
            });

        var result = Assert.Single(results);
        Assert.Equal("tenant-a", result.Id);
        Assert.DoesNotContain(results, item => item.Text.Contains("tenant b", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_FiltersByApplicationAndKnowledgeSource()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var applicationAId = Guid.NewGuid();
        var applicationBId = Guid.NewGuid();
        var sourceAId = Guid.NewGuid();
        var sourceBId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.KnowledgeChunkIndexes.AddRange(
            CreateIndexChunk("app-a", tenantId, folderId, "App A", "shared renewal process", now, applicationAId, sourceAId),
            CreateIndexChunk("app-b", tenantId, folderId, "App B", "shared renewal process secret", now, applicationBId, sourceBId));
        await dbContext.SaveChangesAsync();

        var service = new KnowledgeKeywordIndexService(dbContext);
        var results = await service.SearchAsync(
            ["renewal"],
            10,
            new KnowledgeQueryFilter
            {
                TenantId = tenantId,
                ApplicationId = applicationAId,
                KnowledgeSourceId = sourceAId,
                FolderIds = [folderId],
                IncludeCompanyVisible = true,
                SourceTypes = ["document"],
                Statuses = ["approved"],
            });

        var result = Assert.Single(results);
        Assert.Equal("app-a", result.Id);
        Assert.DoesNotContain(results, item => item.Text.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    private static KnowledgeChunkIndexEntity CreateIndexChunk(
        string id,
        Guid tenantId,
        Guid folderId,
        string title,
        string text,
        DateTimeOffset now,
        Guid? applicationId = null,
        Guid? knowledgeSourceId = null)
    {
        return new KnowledgeChunkIndexEntity
        {
            ChunkId = id,
            TenantId = tenantId,
            ApplicationId = applicationId,
            KnowledgeSourceId = knowledgeSourceId,
            SourceType = KnowledgeSourceType.Document,
            SourceId = $"{id}-source",
            DocumentId = Guid.NewGuid(),
            DocumentVersionId = Guid.NewGuid(),
            FolderId = folderId,
            VisibilityScope = "folder",
            Status = "approved",
            Title = title,
            FolderPath = "/Allowed",
            SectionTitle = "Policy",
            SectionIndex = 0,
            Text = text,
            NormalizedText = text,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
