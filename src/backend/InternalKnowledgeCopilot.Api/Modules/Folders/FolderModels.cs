namespace InternalKnowledgeCopilot.Api.Modules.Folders;

public sealed record FolderTreeItemResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    IReadOnlyList<FolderTreeItemResponse> Children);

public sealed record FolderPermissionResponse(Guid TeamId, string TeamName, bool CanView);

public sealed record FolderDetailResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    IReadOnlyList<FolderPermissionResponse> TeamPermissions);

public sealed record CreateFolderRequest(Guid? ParentId, string Name);

public sealed record UpdateFolderRequest(Guid? ParentId, string Name);

public sealed record FolderTeamPermissionRequest(Guid TeamId, bool CanView);

public sealed record UpdateFolderPermissionsRequest(IReadOnlyList<FolderTeamPermissionRequest> TeamPermissions);
