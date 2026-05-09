namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class FolderPermissionEntity
{
    public Guid Id { get; set; }

    public Guid FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public Guid TeamId { get; set; }

    public TeamEntity? Team { get; set; }

    public bool CanView { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
