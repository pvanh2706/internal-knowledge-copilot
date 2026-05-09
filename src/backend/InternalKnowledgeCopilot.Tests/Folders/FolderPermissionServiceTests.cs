using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Folders;

public sealed class FolderPermissionServiceTests
{
    [Fact]
    public async Task GetVisibleFolderIdsAsync_ReturnsTeamFolders_ForUser()
    {
        await using var dbContext = CreateDbContext();
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedFolderId = Guid.NewGuid();
        var deniedFolderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        dbContext.Teams.Add(new TeamEntity
        {
            Id = teamId,
            Name = "Kỹ thuật",
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
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.Folders.AddRange(
            new FolderEntity
            {
                Id = allowedFolderId,
                Name = "Allowed",
                Path = "/Allowed",
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new FolderEntity
            {
                Id = deniedFolderId,
                Name = "Denied",
                Path = "/Denied",
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        dbContext.FolderPermissions.Add(new FolderPermissionEntity
        {
            Id = Guid.NewGuid(),
            FolderId = allowedFolderId,
            TeamId = teamId,
            CanView = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();

        var service = new FolderPermissionService(dbContext);

        var visibleIds = await service.GetVisibleFolderIdsAsync(userId);

        Assert.Contains(allowedFolderId, visibleIds);
        Assert.DoesNotContain(deniedFolderId, visibleIds);
    }

    [Fact]
    public async Task GetVisibleFolderIdsAsync_ReturnsAllFolders_ForReviewer()
    {
        await using var dbContext = CreateDbContext();
        var reviewerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

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
            Name = "Internal",
            Path = "/Internal",
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync();

        var service = new FolderPermissionService(dbContext);

        var visibleIds = await service.GetVisibleFolderIdsAsync(reviewerId);

        Assert.Contains(folderId, visibleIds);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
