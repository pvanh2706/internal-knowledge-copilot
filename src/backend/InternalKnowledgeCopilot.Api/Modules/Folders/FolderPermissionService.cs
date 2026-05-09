using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Folders;

public interface IFolderPermissionService
{
    Task<bool> CanViewFolderAsync(Guid userId, Guid folderId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetVisibleFolderIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class FolderPermissionService(AppDbContext dbContext) : IFolderPermissionService
{
    public async Task<bool> CanViewFolderAsync(Guid userId, Guid folderId, CancellationToken cancellationToken = default)
    {
        var visibleIds = await GetVisibleFolderIdsAsync(userId, cancellationToken);
        return visibleIds.Contains(folderId);
    }

    public async Task<IReadOnlySet<Guid>> GetVisibleFolderIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && item.DeletedAt == null && item.IsActive, cancellationToken);

        if (user is null)
        {
            return new HashSet<Guid>();
        }

        if (user.Role is UserRole.Admin or UserRole.Reviewer)
        {
            var allFolderIds = await dbContext.Folders
                .AsNoTracking()
                .Where(folder => folder.DeletedAt == null)
                .Select(folder => folder.Id)
                .ToListAsync(cancellationToken);

            return allFolderIds.ToHashSet();
        }

        var visibleByUser = await dbContext.UserFolderPermissions
            .AsNoTracking()
            .Where(permission => permission.UserId == userId && permission.CanView && permission.Folder!.DeletedAt == null)
            .Select(permission => permission.FolderId)
            .ToListAsync(cancellationToken);

        var visibleIds = visibleByUser.ToHashSet();

        if (user.PrimaryTeamId is not null)
        {
            var visibleByTeam = await dbContext.FolderPermissions
                .AsNoTracking()
                .Where(permission => permission.TeamId == user.PrimaryTeamId && permission.CanView && permission.Folder!.DeletedAt == null)
                .Select(permission => permission.FolderId)
                .ToListAsync(cancellationToken);

            visibleIds.UnionWith(visibleByTeam);
        }

        return visibleIds;
    }
}
