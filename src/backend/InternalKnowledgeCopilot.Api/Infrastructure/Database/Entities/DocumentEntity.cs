using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class DocumentEntity
{
    public Guid Id { get; set; }

    public Guid FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public DocumentStatus Status { get; set; }

    public Guid? CurrentVersionId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<DocumentVersionEntity> Versions { get; set; } = [];
}
