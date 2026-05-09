namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public sealed record KnowledgeVectorSearchResult(
    string Id,
    string Text,
    IReadOnlyDictionary<string, object?> Metadata,
    double? Distance);
