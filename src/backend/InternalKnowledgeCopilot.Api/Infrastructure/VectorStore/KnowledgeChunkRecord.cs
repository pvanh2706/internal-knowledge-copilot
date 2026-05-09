namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public sealed record KnowledgeChunkRecord(
    string Id,
    float[] Embedding,
    string Text,
    Dictionary<string, object> Metadata);
