using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Folders;

[ApiController]
[Route("api/folders")]
[Authorize]
public sealed class FoldersController(AppDbContext dbContext, IFolderPermissionService permissionService) : ControllerBase
{
    [HttpGet("tree")]
    public async Task<ActionResult<IReadOnlyList<FolderTreeItemResponse>>> GetTree(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var visibleIds = await permissionService.GetVisibleFolderIdsAsync(userId.Value, cancellationToken);
        var folders = await dbContext.Folders
            .AsNoTracking()
            .Where(folder => folder.DeletedAt == null && visibleIds.Contains(folder.Id))
            .OrderBy(folder => folder.Path)
            .Select(folder => new FolderFlatItem(folder.Id, folder.ParentId, folder.Name, folder.Path))
            .ToListAsync(cancellationToken);

        return Ok(BuildTree(folders));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<ActionResult<FolderDetailResponse>> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (folder is null)
        {
            return NotFound(new ApiError("folder_not_found", "Không tìm thấy folder."));
        }

        var permissions = await dbContext.FolderPermissions
            .AsNoTracking()
            .Include(permission => permission.Team)
            .Where(permission => permission.FolderId == id)
            .OrderBy(permission => permission.Team!.Name)
            .Select(permission => new FolderPermissionResponse(permission.TeamId, permission.Team!.Name, permission.CanView))
            .ToListAsync(cancellationToken);

        return Ok(new FolderDetailResponse(folder.Id, folder.ParentId, folder.Name, folder.Path, permissions));
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<ActionResult<FolderDetailResponse>> Create(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new ApiError("invalid_folder_name", "Tên folder là bắt buộc."));
        }

        var parent = request.ParentId is null
            ? null
            : await dbContext.Folders.FirstOrDefaultAsync(folder => folder.Id == request.ParentId && folder.DeletedAt == null, cancellationToken);

        if (request.ParentId is not null && parent is null)
        {
            return BadRequest(new ApiError("parent_not_found", "Folder cha không tồn tại."));
        }

        var path = BuildPath(parent?.Path, name);
        var exists = await dbContext.Folders.AnyAsync(folder => folder.Path == path && folder.DeletedAt == null, cancellationToken);
        if (exists)
        {
            return Conflict(new ApiError("folder_exists", "Folder đã tồn tại trong cùng vị trí."));
        }

        var now = DateTimeOffset.UtcNow;
        var folderEntity = new FolderEntity
        {
            Id = Guid.NewGuid(),
            ParentId = request.ParentId,
            Name = name,
            Path = path,
            CreatedByUserId = currentUserId.Value,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Folders.Add(folderEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetDetail),
            new { id = folderEntity.Id },
            new FolderDetailResponse(folderEntity.Id, folderEntity.ParentId, folderEntity.Name, folderEntity.Path, []));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<IActionResult> Update(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders.FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);
        if (folder is null)
        {
            return NotFound(new ApiError("folder_not_found", "Không tìm thấy folder."));
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new ApiError("invalid_folder_name", "Tên folder là bắt buộc."));
        }

        if (request.ParentId == id)
        {
            return BadRequest(new ApiError("invalid_parent", "Folder cha không hợp lệ."));
        }

        var parent = request.ParentId is null
            ? null
            : await dbContext.Folders.FirstOrDefaultAsync(item => item.Id == request.ParentId && item.DeletedAt == null, cancellationToken);

        if (request.ParentId is not null && parent is null)
        {
            return BadRequest(new ApiError("parent_not_found", "Folder cha không tồn tại."));
        }

        if (parent is not null && parent.Path.StartsWith(folder.Path + "/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiError("invalid_parent", "Không thể chuyển folder vào chính nhánh con của nó."));
        }

        var oldPath = folder.Path;
        var newPath = BuildPath(parent?.Path, name);
        var pathExists = await dbContext.Folders.AnyAsync(item => item.Id != id && item.Path == newPath && item.DeletedAt == null, cancellationToken);
        if (pathExists)
        {
            return Conflict(new ApiError("folder_exists", "Folder đã tồn tại trong cùng vị trí."));
        }

        folder.Name = name;
        folder.ParentId = request.ParentId;
        folder.Path = newPath;
        folder.UpdatedAt = DateTimeOffset.UtcNow;

        var descendants = await dbContext.Folders
            .Where(item => item.Id != id && item.Path.StartsWith(oldPath + "/"))
            .ToListAsync(cancellationToken);

        foreach (var descendant in descendants)
        {
            descendant.Path = newPath + descendant.Path[oldPath.Length..];
            descendant.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders.FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);
        if (folder is null)
        {
            return NotFound(new ApiError("folder_not_found", "Không tìm thấy folder."));
        }

        var now = DateTimeOffset.UtcNow;
        var foldersToDelete = await dbContext.Folders
            .Where(item => item.Id == id || item.Path.StartsWith(folder.Path + "/"))
            .ToListAsync(cancellationToken);

        foreach (var item in foldersToDelete)
        {
            item.DeletedAt = now;
            item.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateFolderPermissionsRequest request, CancellationToken cancellationToken)
    {
        var folderExists = await dbContext.Folders.AnyAsync(folder => folder.Id == id && folder.DeletedAt == null, cancellationToken);
        if (!folderExists)
        {
            return NotFound(new ApiError("folder_not_found", "Không tìm thấy folder."));
        }

        var teamIds = request.TeamPermissions.Select(permission => permission.TeamId).ToHashSet();
        var existingTeamIds = await dbContext.Teams
            .Where(team => teamIds.Contains(team.Id) && team.DeletedAt == null)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

        if (existingTeamIds.Count != teamIds.Count)
        {
            return BadRequest(new ApiError("team_not_found", "Một hoặc nhiều team không tồn tại."));
        }

        var currentPermissions = await dbContext.FolderPermissions
            .Where(permission => permission.FolderId == id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var requestedPermission in request.TeamPermissions)
        {
            var existingPermission = currentPermissions.FirstOrDefault(permission => permission.TeamId == requestedPermission.TeamId);
            if (existingPermission is null)
            {
                dbContext.FolderPermissions.Add(new FolderPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    FolderId = id,
                    TeamId = requestedPermission.TeamId,
                    CanView = requestedPermission.CanView,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            else
            {
                existingPermission.CanView = requestedPermission.CanView;
                existingPermission.UpdatedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static string BuildPath(string? parentPath, string name)
    {
        var sanitizedName = name.Replace("/", "-", StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(parentPath) ? $"/{sanitizedName}" : $"{parentPath}/{sanitizedName}";
    }

    private static IReadOnlyList<FolderTreeItemResponse> BuildTree(IReadOnlyList<FolderFlatItem> folders)
    {
        var lookup = folders.ToDictionary(
            folder => folder.Id,
            folder => new MutableFolderTreeItem(folder.Id, folder.ParentId, folder.Name, folder.Path));

        var roots = new List<MutableFolderTreeItem>();
        foreach (var item in lookup.Values.OrderBy(item => item.Path))
        {
            if (item.ParentId is not null && lookup.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return roots.Select(ToResponse).ToList();
    }

    private static FolderTreeItemResponse ToResponse(MutableFolderTreeItem item)
    {
        return new FolderTreeItemResponse(
            item.Id,
            item.ParentId,
            item.Name,
            item.Path,
            item.Children.OrderBy(child => child.Name).Select(ToResponse).ToList());
    }

    private sealed record FolderFlatItem(Guid Id, Guid? ParentId, string Name, string Path);

    private sealed class MutableFolderTreeItem(Guid id, Guid? parentId, string name, string path)
    {
        public Guid Id { get; } = id;

        public Guid? ParentId { get; } = parentId;

        public string Name { get; } = name;

        public string Path { get; } = path;

        public List<MutableFolderTreeItem> Children { get; } = [];
    }
}
