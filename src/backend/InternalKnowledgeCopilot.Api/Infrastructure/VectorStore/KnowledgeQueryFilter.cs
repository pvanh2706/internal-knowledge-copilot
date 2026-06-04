namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public sealed record KnowledgeQueryFilter
{
    public Guid? TenantId { get; init; }

    public Guid? ApplicationId { get; init; }

    public Guid? KnowledgeSourceId { get; init; }

    public string? ExternalObjectType { get; init; }

    public string? ExternalObjectId { get; init; }

    public IReadOnlyCollection<Guid> FolderIds { get; init; } = [];

    public Guid? DocumentId { get; init; }

    public bool IncludeCompanyVisible { get; init; }

    public IReadOnlyCollection<string> SourceTypes { get; init; } = [];

    public IReadOnlyCollection<string> Statuses { get; init; } = [];
}
