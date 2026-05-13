using System.Globalization;
using System.Text;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;

public interface IKnowledgeKeywordIndexService
{
    Task ReplaceChunksAsync(KnowledgeSourceType sourceType, string sourceId, IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeVectorSearchResult>> SearchAsync(
        IReadOnlyList<string> keywords,
        int limit,
        KnowledgeQueryFilter filter,
        CancellationToken cancellationToken = default);
}

public sealed class KnowledgeKeywordIndexService(AppDbContext dbContext) : IKnowledgeKeywordIndexService
{
    public async Task ReplaceChunksAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeChunkRecord> chunks,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.KnowledgeChunkIndexes
            .Where(chunk => chunk.SourceType == sourceType && chunk.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        dbContext.KnowledgeChunkIndexes.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        dbContext.KnowledgeChunkIndexes.AddRange(chunks.Select(chunk => ToEntity(sourceType, sourceId, chunk, now)));
    }

    public async Task<IReadOnlyList<KnowledgeVectorSearchResult>> SearchAsync(
        IReadOnlyList<string> keywords,
        int limit,
        KnowledgeQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeywords = keywords
            .Select(NormalizeForSearch)
            .Where(keyword => keyword.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();

        if (normalizedKeywords.Length == 0 || limit <= 0)
        {
            return [];
        }

        var query = ApplyFilter(dbContext.KnowledgeChunkIndexes.AsNoTracking(), filter);
        var candidates = await query.ToListAsync(cancellationToken);

        return candidates
            .Select(candidate => new KeywordCandidate(candidate, Score(candidate, normalizedKeywords)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Chunk.UpdatedAt)
            .Take(limit)
            .Select(candidate => ToSearchResult(candidate.Chunk))
            .ToList();
    }

    private static IQueryable<KnowledgeChunkIndexEntity> ApplyFilter(
        IQueryable<KnowledgeChunkIndexEntity> query,
        KnowledgeQueryFilter filter)
    {
        var sourceTypes = filter.SourceTypes
            .Select(value => Enum.TryParse<KnowledgeSourceType>(value, true, out var sourceType) ? sourceType : (KnowledgeSourceType?)null)
            .Where(sourceType => sourceType is not null)
            .Select(sourceType => sourceType!.Value)
            .ToArray();
        if (sourceTypes.Length > 0)
        {
            query = query.Where(chunk => sourceTypes.Contains(chunk.SourceType));
        }

        var statuses = filter.Statuses
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (statuses.Length > 0)
        {
            query = query.Where(chunk => statuses.Contains(chunk.Status.ToLower()));
        }

        if (filter.DocumentId is not null)
        {
            query = query.Where(chunk => chunk.DocumentId == filter.DocumentId);
        }

        var folderIds = filter.FolderIds.Distinct().ToArray();
        if (filter.IncludeCompanyVisible)
        {
            query = folderIds.Length == 0
                ? query.Where(chunk => chunk.VisibilityScope == "company")
                : query.Where(chunk => chunk.VisibilityScope == "company" || (chunk.FolderId != null && folderIds.Contains(chunk.FolderId.Value)));
        }
        else
        {
            query = folderIds.Length == 0
                ? query.Where(chunk => false)
                : query.Where(chunk => chunk.FolderId != null && folderIds.Contains(chunk.FolderId.Value));
        }

        return query;
    }

    private static int Score(KnowledgeChunkIndexEntity chunk, IReadOnlyList<string> normalizedKeywords)
    {
        var score = 0;
        var title = NormalizeForSearch($"{chunk.Title} {chunk.SectionTitle}");

        foreach (var keyword in normalizedKeywords)
        {
            if (chunk.NormalizedText.Contains(keyword, StringComparison.Ordinal))
            {
                score += 10;
            }

            if (title.Contains(keyword, StringComparison.Ordinal))
            {
                score += 4;
            }
        }

        if (normalizedKeywords.Count > 1 && chunk.NormalizedText.Contains(string.Join(' ', normalizedKeywords), StringComparison.Ordinal))
        {
            score += 20;
        }

        return score;
    }

    private static KnowledgeChunkIndexEntity ToEntity(KnowledgeSourceType sourceType, string sourceId, KnowledgeChunkRecord chunk, DateTimeOffset now)
    {
        return new KnowledgeChunkIndexEntity
        {
            ChunkId = chunk.Id,
            SourceType = sourceType,
            SourceId = sourceId,
            DocumentId = GetGuid(chunk.Metadata, "document_id"),
            DocumentVersionId = GetGuid(chunk.Metadata, "document_version_id"),
            WikiPageId = GetGuid(chunk.Metadata, "wiki_page_id"),
            FolderId = GetGuid(chunk.Metadata, "folder_id"),
            VisibilityScope = GetString(chunk.Metadata, "visibility_scope")?.ToLowerInvariant() ?? "folder",
            Status = GetString(chunk.Metadata, "status")?.ToLowerInvariant() ?? string.Empty,
            Title = GetString(chunk.Metadata, "title") ?? "Nguon tri thuc",
            FolderPath = GetString(chunk.Metadata, "folder_path") ?? string.Empty,
            SectionTitle = GetString(chunk.Metadata, "section_title"),
            SectionIndex = GetInt(chunk.Metadata, "section_index"),
            Text = chunk.Text,
            NormalizedText = NormalizeForSearch($"{GetString(chunk.Metadata, "title")} {GetString(chunk.Metadata, "section_title")} {chunk.Text}"),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static KnowledgeVectorSearchResult ToSearchResult(KnowledgeChunkIndexEntity chunk)
    {
        return new KnowledgeVectorSearchResult(
            chunk.ChunkId,
            chunk.Text,
            new Dictionary<string, object?>
            {
                ["chunk_id"] = chunk.ChunkId,
                ["source_type"] = chunk.SourceType.ToString().ToLowerInvariant(),
                ["source_id"] = chunk.SourceId,
                ["document_id"] = chunk.DocumentId?.ToString() ?? string.Empty,
                ["document_version_id"] = chunk.DocumentVersionId?.ToString() ?? string.Empty,
                ["wiki_page_id"] = chunk.WikiPageId?.ToString() ?? string.Empty,
                ["folder_id"] = chunk.FolderId?.ToString() ?? string.Empty,
                ["visibility_scope"] = chunk.VisibilityScope,
                ["status"] = chunk.Status,
                ["title"] = chunk.Title,
                ["folder_path"] = chunk.FolderPath,
                ["section_title"] = chunk.SectionTitle ?? string.Empty,
                ["section_index"] = chunk.SectionIndex ?? -1,
            },
            null);
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
        return int.TryParse(value, out var number) ? number : null;
    }

    private static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record KeywordCandidate(KnowledgeChunkIndexEntity Chunk, int Score);
}
