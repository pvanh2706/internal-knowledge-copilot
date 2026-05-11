namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public sealed record KnowledgeQueryFilter
{
    public IReadOnlyCollection<Guid> FolderIds { get; init; } = [];

    public Guid? DocumentId { get; init; }

    public bool IncludeCompanyVisible { get; init; }

    public IReadOnlyCollection<string> SourceTypes { get; init; } = [];

    public IReadOnlyCollection<string> Statuses { get; init; } = [];
}
