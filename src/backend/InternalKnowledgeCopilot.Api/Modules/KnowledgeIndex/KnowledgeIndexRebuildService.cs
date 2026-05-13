using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;

public interface IKnowledgeIndexRebuildService
{
    Task<KnowledgeIndexSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<RebuildKnowledgeIndexResponse> RebuildAsync(
        Guid actorUserId,
        RebuildKnowledgeIndexRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class KnowledgeIndexRebuildService(
    AppDbContext dbContext,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IKnowledgeKeywordIndexService keywordIndexService,
    IAuditLogService auditLogService) : IKnowledgeIndexRebuildService
{
    private const int DefaultBatchSize = 50;
    private const int MaxBatchSize = 200;

    public async Task<KnowledgeIndexSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var ledgerSourceCountRows = await dbContext.KnowledgeChunks
            .AsNoTracking()
            .GroupBy(chunk => chunk.SourceType)
            .Select(group => new { SourceType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var ledgerSourceCounts = ledgerSourceCountRows
            .Select(item => new KnowledgeIndexSourceCountResponse(item.SourceType.ToString(), item.Count))
            .OrderBy(item => item.SourceType)
            .ToList();

        return new KnowledgeIndexSummaryResponse(
            await dbContext.KnowledgeChunks.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.KnowledgeChunkIndexes.AsNoTracking().CountAsync(cancellationToken),
            ledgerSourceCounts);
    }

    public async Task<RebuildKnowledgeIndexResponse> RebuildAsync(
        Guid actorUserId,
        RebuildKnowledgeIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var batchSize = Math.Clamp(request.BatchSize <= 0 ? DefaultBatchSize : request.BatchSize, 1, MaxBatchSize);
        var ledgerChunks = await dbContext.KnowledgeChunks
            .AsNoTracking()
            .OrderBy(chunk => chunk.SourceType)
            .ThenBy(chunk => chunk.SourceId)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ThenBy(chunk => chunk.ChunkId)
            .ToListAsync(cancellationToken);

        if (request.ResetVectorStore)
        {
            await vectorStore.ResetCollectionAsync(cancellationToken);
        }

        var records = new List<KnowledgeChunkRecord>(ledgerChunks.Count);
        foreach (var chunk in ledgerChunks)
        {
            records.Add(new KnowledgeChunkRecord(
                chunk.VectorId,
                await embeddingService.CreateEmbeddingAsync(chunk.Text, cancellationToken),
                chunk.Text,
                ReadMetadata(chunk)));
        }

        var batchCount = 0;
        foreach (var batch in records.Chunk(batchSize))
        {
            await vectorStore.UpsertChunksAsync(batch, cancellationToken);
            batchCount += 1;
        }

        foreach (var group in records.GroupBy(record => new SourceKey(
            ParseSourceType(record.Metadata),
            GetMetadataString(record.Metadata, "source_id") ?? record.Id)))
        {
            await keywordIndexService.ReplaceChunksAsync(group.Key.SourceType, group.Key.SourceId, group.ToList(), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var sourceCounts = ledgerChunks
            .GroupBy(chunk => chunk.SourceType)
            .Select(group => new KnowledgeIndexSourceCountResponse(group.Key.ToString(), group.Count()))
            .OrderBy(item => item.SourceType)
            .ToList();
        var finishedAt = DateTimeOffset.UtcNow;
        var response = new RebuildKnowledgeIndexResponse(
            ledgerChunks.Count,
            records.Count,
            batchCount,
            request.ResetVectorStore,
            sourceCounts,
            startedAt,
            finishedAt);

        await auditLogService.RecordAsync(
            actorUserId,
            "KnowledgeIndexRebuilt",
            "KnowledgeIndex",
            null,
            new
            {
                response.TotalLedgerChunks,
                response.RebuiltChunks,
                response.BatchCount,
                response.ResetVectorStore,
            },
            cancellationToken);

        return response;
    }

    private static Dictionary<string, object> ReadMetadata(KnowledgeChunkEntity chunk)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(chunk.MetadataJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var value = ReadMetadataValue(property.Value);
                    if (value is not null)
                    {
                        metadata[property.Name] = value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // The ledger columns below are enough to rebuild a valid index record.
        }

        metadata["chunk_id"] = chunk.ChunkId;
        metadata["source_type"] = chunk.SourceType.ToString().ToLowerInvariant();
        metadata["source_id"] = chunk.SourceId;
        metadata["visibility_scope"] = chunk.VisibilityScope;
        metadata["status"] = chunk.Status;
        metadata["title"] = chunk.Title;
        metadata["folder_path"] = chunk.FolderPath;
        metadata["chunk_index"] = chunk.ChunkIndex;

        AddOptional(metadata, "document_id", chunk.DocumentId);
        AddOptional(metadata, "document_version_id", chunk.DocumentVersionId);
        AddOptional(metadata, "wiki_page_id", chunk.WikiPageId);
        AddOptional(metadata, "correction_id", chunk.CorrectionId);
        AddOptional(metadata, "folder_id", chunk.FolderId);
        AddOptional(metadata, "section_title", chunk.SectionTitle);
        AddOptional(metadata, "section_index", chunk.SectionIndex);
        AddOptional(metadata, "text_hash", chunk.TextHash);

        return metadata;
    }

    private static object? ReadMetadataValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static void AddOptional(Dictionary<string, object> metadata, string key, Guid? value)
    {
        if (value is not null)
        {
            metadata[key] = value.Value.ToString();
        }
    }

    private static void AddOptional(Dictionary<string, object> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static void AddOptional(Dictionary<string, object> metadata, string key, int? value)
    {
        if (value is not null)
        {
            metadata[key] = value.Value;
        }
    }

    private static KnowledgeSourceType ParseSourceType(IReadOnlyDictionary<string, object> metadata)
    {
        return Enum.TryParse<KnowledgeSourceType>(GetMetadataString(metadata, "source_type"), true, out var sourceType)
            ? sourceType
            : KnowledgeSourceType.Document;
    }

    private static string? GetMetadataString(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private sealed record SourceKey(KnowledgeSourceType SourceType, string SourceId);
}
