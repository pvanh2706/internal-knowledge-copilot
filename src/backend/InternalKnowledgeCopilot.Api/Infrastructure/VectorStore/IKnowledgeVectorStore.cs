namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public interface IKnowledgeVectorStore
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

    Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(
        float[] embedding,
        int limit,
        KnowledgeQueryFilter? filter = null,
        CancellationToken cancellationToken = default);
}
