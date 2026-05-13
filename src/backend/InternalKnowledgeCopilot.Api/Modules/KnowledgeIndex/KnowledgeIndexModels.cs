namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;

public sealed record RebuildKnowledgeIndexRequest(
    bool ResetVectorStore = false,
    int BatchSize = 50);

public sealed record RebuildKnowledgeIndexResponse(
    int TotalLedgerChunks,
    int RebuiltChunks,
    int BatchCount,
    bool ResetVectorStore,
    IReadOnlyList<KnowledgeIndexSourceCountResponse> SourceCounts,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);

public sealed record KnowledgeIndexSummaryResponse(
    int LedgerChunkCount,
    int KeywordIndexChunkCount,
    IReadOnlyList<KnowledgeIndexSourceCountResponse> LedgerSourceCounts);

public sealed record KnowledgeIndexSourceCountResponse(
    string SourceType,
    int Count);
