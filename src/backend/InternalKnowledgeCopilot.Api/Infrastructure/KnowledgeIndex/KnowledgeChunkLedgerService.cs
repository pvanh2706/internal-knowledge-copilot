using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;

public interface IKnowledgeChunkLedgerService
{
    Task ReplaceChunksAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeChunkRecord> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeChunkSnapshot>> GetChunksForSourceAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        CancellationToken cancellationToken = default);
}

public sealed class KnowledgeChunkLedgerService(AppDbContext dbContext) : IKnowledgeChunkLedgerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ReplaceChunksAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeChunkRecord> chunks,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.KnowledgeChunks
            .Where(chunk => chunk.SourceType == sourceType && chunk.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        dbContext.KnowledgeChunks.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        dbContext.KnowledgeChunks.AddRange(chunks.Select(chunk => ToEntity(sourceType, sourceId, chunk, now)));
    }

    public async Task<IReadOnlyList<KnowledgeChunkSnapshot>> GetChunksForSourceAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(chunk => chunk.SourceType == sourceType && chunk.SourceId == sourceId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ThenBy(chunk => chunk.ChunkId)
            .Select(chunk => new KnowledgeChunkSnapshot(
                chunk.ChunkId,
                chunk.SourceType,
                chunk.SourceId,
                chunk.Title,
                chunk.SectionTitle,
                chunk.ChunkIndex,
                chunk.Text,
                chunk.TextHash,
                chunk.VectorId,
                chunk.MetadataJson,
                chunk.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    private static KnowledgeChunkEntity ToEntity(
        KnowledgeSourceType sourceType,
        string sourceId,
        KnowledgeChunkRecord chunk,
        DateTimeOffset now)
    {
        return new KnowledgeChunkEntity
        {
            ChunkId = chunk.Id,
            SourceType = sourceType,
            SourceId = sourceId,
            DocumentId = GetGuid(chunk.Metadata, "document_id"),
            DocumentVersionId = GetGuid(chunk.Metadata, "document_version_id"),
            WikiPageId = GetGuid(chunk.Metadata, "wiki_page_id"),
            CorrectionId = GetGuid(chunk.Metadata, "correction_id"),
            FolderId = GetGuid(chunk.Metadata, "folder_id"),
            VisibilityScope = GetString(chunk.Metadata, "visibility_scope")?.ToLowerInvariant() ?? "folder",
            Status = GetString(chunk.Metadata, "status")?.ToLowerInvariant() ?? string.Empty,
            Title = GetString(chunk.Metadata, "title") ?? "Nguon tri thuc",
            FolderPath = GetString(chunk.Metadata, "folder_path") ?? string.Empty,
            SectionTitle = NormalizeOptional(GetString(chunk.Metadata, "section_title")),
            SectionIndex = GetInt(chunk.Metadata, "section_index"),
            ChunkIndex = GetInt(chunk.Metadata, "chunk_index") ?? 0,
            Text = chunk.Text,
            TextHash = ComputeTextHash(chunk.Text),
            VectorId = chunk.Id,
            MetadataJson = JsonSerializer.Serialize(chunk.Metadata, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static Guid? GetGuid(IReadOnlyDictionary<string, object> metadata, string key)
    {
        var value = GetString(metadata, key);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static int? GetInt(IReadOnlyDictionary<string, object> metadata, string key)
    {
        var value = GetString(metadata, key);
        return int.TryParse(value, out var number) && number >= 0 ? number : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ComputeTextHash(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}

public sealed record KnowledgeChunkSnapshot(
    string ChunkId,
    KnowledgeSourceType SourceType,
    string SourceId,
    string Title,
    string? SectionTitle,
    int ChunkIndex,
    string Text,
    string TextHash,
    string VectorId,
    string MetadataJson,
    DateTimeOffset UpdatedAt);
