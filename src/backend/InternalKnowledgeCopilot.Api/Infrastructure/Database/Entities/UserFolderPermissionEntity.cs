namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class UserFolderPermissionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserEntity? User { get; set; }

    public Guid FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public bool CanView { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
