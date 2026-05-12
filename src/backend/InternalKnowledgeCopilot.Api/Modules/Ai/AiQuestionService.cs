using System.Diagnostics;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Ai;

public interface IAiQuestionService
{
    Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiQuestionService(
    AppDbContext dbContext,
    IFolderPermissionService folderPermissionService,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IAnswerGenerationService answerGenerationService) : IAiQuestionService
{
    private const int SearchLimit = 30;
    private const int MaxContextChunks = 5;

    public async Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("question_required");
        }

        await ValidateScopeAsync(userId, request, cancellationToken);

        var visibleFolderIds = await folderPermissionService.GetVisibleFolderIdsAsync(userId, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var queryEmbedding = await embeddingService.CreateEmbeddingAsync(question, cancellationToken);
        var vectorResults = await vectorStore.QueryAsync(
            queryEmbedding,
            SearchLimit,
            BuildKnowledgeQueryFilter(visibleFolderIds, request),
            cancellationToken);
        var chunks = await FilterAllowedChunksAsync(vectorResults, visibleFolderIds, request, cancellationToken);

        chunks = chunks
            .OrderByDescending(chunk => chunk.SourceType == KnowledgeSourceType.Wiki)
            .ThenBy(chunk => chunk.Distance ?? double.MaxValue)
            .Take(MaxContextChunks)
            .ToList();

        var answerDraft = await answerGenerationService.GenerateAsync(question, chunks, cancellationToken);
        var citedSourceIdSet = answerDraft.CitedSourceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var citedChunks = citedSourceIdSet.Count == 0
            ? chunks
            : chunks.Where(chunk => citedSourceIdSet.Contains(chunk.SourceId)).ToList();
        stopwatch.Stop();

        var now = DateTimeOffset.UtcNow;
        var interactionId = Guid.NewGuid();
        var interaction = new AiInteractionEntity
        {
            Id = interactionId,
            UserId = userId,
            Question = question,
            Answer = answerDraft.Answer,
            ScopeType = request.ScopeType,
            ScopeFolderId = request.FolderId,
            ScopeDocumentId = request.DocumentId,
            NeedsClarification = answerDraft.NeedsClarification,
            Confidence = answerDraft.Confidence,
            MissingInformationJson = JsonSerializer.Serialize(answerDraft.MissingInformation),
            ConflictsJson = JsonSerializer.Serialize(answerDraft.Conflicts),
            SuggestedFollowUpsJson = JsonSerializer.Serialize(answerDraft.SuggestedFollowUps),
            LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
            UsedWikiCount = citedChunks.Count(chunk => chunk.SourceType == KnowledgeSourceType.Wiki),
            UsedDocumentCount = citedChunks.Count(chunk => chunk.SourceType == KnowledgeSourceType.Document),
            CreatedAt = now,
        };

        dbContext.AiInteractions.Add(interaction);
        dbContext.AiInteractionSources.AddRange(citedChunks.Select((chunk, index) => new AiInteractionSourceEntity
        {
            Id = Guid.NewGuid(),
            AiInteractionId = interactionId,
            SourceType = chunk.SourceType,
            SourceId = chunk.SourceId,
            DocumentId = chunk.DocumentId,
            DocumentVersionId = chunk.DocumentVersionId,
            WikiPageId = chunk.WikiPageId,
            Title = chunk.Title,
            FolderPath = chunk.FolderPath,
            SectionTitle = chunk.SectionTitle,
            Excerpt = ToExcerpt(chunk.Text),
            Rank = index + 1,
            CreatedAt = now,
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AskQuestionResponse(
            interactionId,
            answerDraft.Answer,
            answerDraft.NeedsClarification,
            answerDraft.Confidence,
            answerDraft.MissingInformation,
            answerDraft.Conflicts,
            answerDraft.SuggestedFollowUps,
            citedChunks.Select(chunk => new AiCitationResponse(
                chunk.SourceType,
                chunk.Title,
                chunk.FolderPath,
                chunk.SectionTitle,
                ToExcerpt(chunk.Text))).ToList());
    }

    private async Task ValidateScopeAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken)
    {
        if (request.ScopeType == AiScopeType.Folder)
        {
            if (request.FolderId is null)
            {
                throw new ArgumentException("folder_required");
            }

            if (!await folderPermissionService.CanViewFolderAsync(userId, request.FolderId.Value, cancellationToken))
            {
                throw new UnauthorizedAccessException("folder_forbidden");
            }
        }

        if (request.ScopeType == AiScopeType.Document)
        {
            if (request.DocumentId is null)
            {
                throw new ArgumentException("document_required");
            }

            var document = await dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.DocumentId && item.DeletedAt == null, cancellationToken);

            if (document is null)
            {
                throw new KeyNotFoundException("document_not_found");
            }

            if (!await folderPermissionService.CanViewFolderAsync(userId, document.FolderId, cancellationToken))
            {
                throw new UnauthorizedAccessException("document_forbidden");
            }
        }
    }

    private static KnowledgeQueryFilter BuildKnowledgeQueryFilter(IReadOnlySet<Guid> visibleFolderIds, AskQuestionRequest request)
    {
        var sourceTypes = new[] { "document", "wiki" };
        var statuses = new[] { "approved", "published" };

        return request.ScopeType switch
        {
            AiScopeType.Folder => new KnowledgeQueryFilter
            {
                FolderIds = request.FolderId is null ? [] : [request.FolderId.Value],
                IncludeCompanyVisible = false,
                SourceTypes = sourceTypes,
                Statuses = statuses,
            },
            AiScopeType.Document => new KnowledgeQueryFilter
            {
                FolderIds = visibleFolderIds.ToArray(),
                DocumentId = request.DocumentId,
                IncludeCompanyVisible = true,
                SourceTypes = sourceTypes,
                Statuses = statuses,
            },
            _ => new KnowledgeQueryFilter
            {
                FolderIds = visibleFolderIds.ToArray(),
                IncludeCompanyVisible = true,
                SourceTypes = sourceTypes,
                Statuses = statuses,
            },
        };
    }

    private async Task<List<RetrievedKnowledgeChunk>> FilterAllowedChunksAsync(
        IReadOnlyList<KnowledgeVectorSearchResult> vectorResults,
        IReadOnlySet<Guid> visibleFolderIds,
        AskQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var candidates = vectorResults
            .Select(result => ToRetrievedChunk(result))
            .Where(chunk => chunk is not null)
            .Select(chunk => chunk!)
            .Where(chunk => IsInRequestedScope(chunk, request))
            .Where(chunk => IsVisibleByFolder(chunk, visibleFolderIds))
            .ToList();

        var documentVersionIds = candidates
            .Where(chunk => chunk.SourceType == KnowledgeSourceType.Document && chunk.DocumentVersionId is not null)
            .Select(chunk => chunk.DocumentVersionId!.Value)
            .ToHashSet();
        var wikiPageIds = candidates
            .Where(chunk => chunk.SourceType == KnowledgeSourceType.Wiki && chunk.WikiPageId is not null)
            .Select(chunk => chunk.WikiPageId!.Value)
            .ToHashSet();

        var currentIndexedVersionIds = await dbContext.DocumentVersions
            .AsNoTracking()
            .Include(version => version.Document)
            .Where(version =>
                documentVersionIds.Contains(version.Id) &&
                version.Status == DocumentVersionStatus.Indexed &&
                version.Document != null &&
                version.Document.CurrentVersionId == version.Id &&
                version.Document.DeletedAt == null &&
                visibleFolderIds.Contains(version.Document.FolderId))
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);

        var currentVersionIdSet = currentIndexedVersionIds.ToHashSet();
        var visibleWikiPages = await dbContext.WikiPages
            .AsNoTracking()
            .Where(page =>
                wikiPageIds.Contains(page.Id) &&
                page.ArchivedAt == null &&
                (
                    (page.VisibilityScope == VisibilityScope.Company && page.IsCompanyPublicConfirmed) ||
                    (page.VisibilityScope == VisibilityScope.Folder && page.FolderId != null && visibleFolderIds.Contains(page.FolderId.Value))
                ))
            .Select(page => new WikiPageVisibility(page.Id, page.SourceDocumentId, page.VisibilityScope, page.FolderId))
            .ToDictionaryAsync(page => page.Id, cancellationToken);

        return candidates
            .Where(chunk => chunk.SourceType switch
            {
                KnowledgeSourceType.Wiki => IsAllowedWikiChunk(chunk, visibleWikiPages, request),
                KnowledgeSourceType.Document => chunk.DocumentVersionId is not null && currentVersionIdSet.Contains(chunk.DocumentVersionId.Value),
                _ => false,
            })
            .ToList();
    }

    private static bool IsAllowedWikiChunk(
        RetrievedKnowledgeChunk chunk,
        IReadOnlyDictionary<Guid, WikiPageVisibility> visibleWikiPages,
        AskQuestionRequest request)
    {
        if (chunk.WikiPageId is null || !visibleWikiPages.TryGetValue(chunk.WikiPageId.Value, out var page))
        {
            return false;
        }

        if (request.ScopeType == AiScopeType.Folder && page.FolderId != request.FolderId)
        {
            return false;
        }

        if (request.ScopeType == AiScopeType.Document && page.SourceDocumentId != request.DocumentId)
        {
            return false;
        }

        if (page.VisibilityScope == VisibilityScope.Folder && chunk.FolderId != page.FolderId)
        {
            return false;
        }

        return true;
    }

    private static RetrievedKnowledgeChunk? ToRetrievedChunk(KnowledgeVectorSearchResult result)
    {
        var sourceTypeText = GetString(result.Metadata, "source_type");
        if (!Enum.TryParse<KnowledgeSourceType>(sourceTypeText, true, out var sourceType))
        {
            return null;
        }

        var sourceId = GetString(result.Metadata, "source_id");
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            sourceId = result.Id;
        }

        return new RetrievedKnowledgeChunk(
            sourceType,
            sourceId,
            GetGuid(result.Metadata, "document_id"),
            GetGuid(result.Metadata, "document_version_id"),
            GetGuid(result.Metadata, "wiki_page_id"),
            GetGuid(result.Metadata, "folder_id"),
            GetString(result.Metadata, "visibility_scope"),
            GetString(result.Metadata, "title") ?? "Nguồn tri thức",
            GetString(result.Metadata, "folder_path") ?? string.Empty,
            GetString(result.Metadata, "section_title"),
            GetInt(result.Metadata, "section_index"),
            result.Text,
            result.Distance);
    }

    private static bool IsInRequestedScope(RetrievedKnowledgeChunk chunk, AskQuestionRequest request)
    {
        return request.ScopeType switch
        {
            AiScopeType.All => true,
            AiScopeType.Folder => chunk.FolderId == request.FolderId,
            AiScopeType.Document => chunk.DocumentId == request.DocumentId,
            _ => false,
        };
    }

    private static bool IsVisibleByFolder(RetrievedKnowledgeChunk chunk, IReadOnlySet<Guid> visibleFolderIds)
    {
        if (chunk.SourceType == KnowledgeSourceType.Wiki && string.Equals(chunk.VisibilityScope, "company", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return chunk.FolderId is not null && visibleFolderIds.Contains(chunk.FolderId.Value);
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static Guid? GetGuid(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        var value = GetString(metadata, key);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static int? GetInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        var value = GetString(metadata, key);
        return int.TryParse(value, out var number) ? number : null;
    }

    private static string ToExcerpt(string text)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 320 ? normalized : normalized[..320].TrimEnd() + "...";
    }

    private sealed record WikiPageVisibility(Guid Id, Guid SourceDocumentId, VisibilityScope VisibilityScope, Guid? FolderId);
}
