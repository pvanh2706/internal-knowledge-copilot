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
        var citation = Assert.Single(response.Citations);
        Assert.Equal("Allowed payment", citation.Title);
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
        Assert.Empty(response.Citations);
        Assert.DoesNotContain("secret wiki answer", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.AiInteractions.CountAsync());
        Assert.Equal(0, await dbContext.AiInteractionSources.CountAsync());
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

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeKnowledgeVectorStore(IReadOnlyList<KnowledgeVectorSearchResult> results) : IKnowledgeVectorStore
    {
        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(float[] embedding, int limit, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(results);
        }
    }
}
