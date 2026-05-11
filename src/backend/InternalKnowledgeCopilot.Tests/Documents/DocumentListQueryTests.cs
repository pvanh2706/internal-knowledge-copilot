using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Modules.Documents;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Documents;

public sealed class DocumentListQueryTests
{
    [Fact]
    public async Task GetDocuments_UsesClientSideDateOrdering_ForSqliteDateTimeOffset()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var olderDocumentId = Guid.NewGuid();
        var newerDocumentId = Guid.NewGuid();

        dbContext.Teams.Add(new TeamEntity
        {
            Id = teamId,
            Name = "Ky thuat",
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.local",
            DisplayName = "User",
            PasswordHash = "hash",
            Role = UserRole.User,
            PrimaryTeamId = teamId,
            MustChangePassword = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.Add(new FolderEntity
        {
            Id = folderId,
            Name = "Policies",
            Path = "/Policies",
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
        AddDocument(dbContext, olderDocumentId, folderId, userId, "Older", now.AddDays(-2));
        AddDocument(dbContext, newerDocumentId, folderId, userId, "Newer", now.AddDays(-1));
        await dbContext.SaveChangesAsync();

        var controller = new DocumentsController(
            dbContext,
            new FolderPermissionService(dbContext),
            new NoopFileUploadValidator(),
            new NoopFileStorageService(),
            new NoopAuditLogService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, nameof(UserRole.User)),
                    ], "Test")),
                },
            },
        };

        var response = await controller.GetDocuments(null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var documents = Assert.IsAssignableFrom<IReadOnlyList<DocumentListItemResponse>>(ok.Value);
        Assert.Equal(["Newer", "Older"], documents.Select(document => document.Title).ToArray());
    }

    private static void AddDocument(AppDbContext dbContext, Guid documentId, Guid folderId, Guid userId, string title, DateTimeOffset updatedAt)
    {
        var versionId = Guid.NewGuid();
        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            FolderId = folderId,
            Title = title,
            Status = DocumentStatus.Approved,
            CurrentVersionId = versionId,
            CreatedByUserId = userId,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        dbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            VersionNumber = 1,
            OriginalFileName = $"{title}.txt",
            StoredFilePath = $"{title}.txt",
            FileExtension = ".txt",
            FileSizeBytes = 10,
            Status = DocumentVersionStatus.Indexed,
            UploadedByUserId = userId,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
    }

    private sealed class NoopFileUploadValidator : IFileUploadValidator
    {
        public FileValidationResult Validate(IFormFile? file) => FileValidationResult.Valid();
    }

    private sealed class NoopFileStorageService : IFileStorageService
    {
        public Task<string> SaveDocumentVersionAsync(Guid documentId, Guid versionId, IFormFile file, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("stored.txt");
        }

        public bool TryResolveStoredPath(string storedPath, out string resolvedPath)
        {
            resolvedPath = storedPath;
            return true;
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
