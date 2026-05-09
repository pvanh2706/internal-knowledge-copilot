namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class FolderEntity
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public FolderEntity? Parent { get; set; }

    public List<FolderEntity> Children { get; set; } = [];

    public required string Name { get; set; }

    public required string Path { get; set; }

    public Guid CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<FolderPermissionEntity> TeamPermissions { get; set; } = [];

    public List<UserFolderPermissionEntity> UserPermissions { get; set; } = [];
}
