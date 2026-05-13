using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Ai;

public interface IAiQuestionService
{
    Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default);

    Task<RetrievalExplainResponse> ExplainRetrievalAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiQuestionService(
    AppDbContext dbContext,
    IFolderPermissionService folderPermissionService,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IKnowledgeKeywordIndexService keywordIndexService,
    IAnswerGenerationService answerGenerationService) : IAiQuestionService
{
    private const int SearchLimit = 50;
    private const int KeywordSearchLimit = 20;
    private const int MaxContextChunks = 8;
    private const int MaxChunksPerKnowledgeItem = 3;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were", "has", "have", "not",
        "mot", "cac", "cua", "cho", "voi", "khi", "thi", "la", "va", "de", "duoc", "trong", "ngoai",
        "neu", "sau", "truoc", "phai", "can", "nen", "hoi", "tra", "loi", "nguoi", "dung", "nhung",
        "nao", "gi", "tai", "sao", "hay", "ve", "vao", "ra", "len", "xuong", "noi", "bo",
    };

    public async Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("question_required");
        }

        await ValidateScopeAsync(userId, request, cancellationToken);

        var queryUnderstanding = UnderstandQuery(question);
        var visibleFolderIds = await folderPermissionService.GetVisibleFolderIdsAsync(userId, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var queryEmbedding = await embeddingService.CreateEmbeddingAsync(question, cancellationToken);
        var knowledgeFilter = BuildKnowledgeQueryFilter(visibleFolderIds, request);
        var vectorResults = await vectorStore.QueryAsync(
            queryEmbedding,
            SearchLimit,
            knowledgeFilter,
            cancellationToken);
        var keywordResults = await keywordIndexService.SearchAsync(
            queryUnderstanding.Keywords,
            KeywordSearchLimit,
            knowledgeFilter,
            cancellationToken);
        var chunks = await FilterAllowedChunksAsync(
            MergeCandidateResults(vectorResults, keywordResults),
            visibleFolderIds,
            request,
            cancellationToken);

        chunks = RerankAndPackContext(chunks, queryUnderstanding, request);

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

    public async Task<RetrievalExplainResponse> ExplainRetrievalAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("question_required");
        }

        await ValidateScopeAsync(userId, request, cancellationToken);

        var queryUnderstanding = UnderstandQuery(question);
        var visibleFolderIds = await folderPermissionService.GetVisibleFolderIdsAsync(userId, cancellationToken);
        var queryEmbedding = await embeddingService.CreateEmbeddingAsync(question, cancellationToken);
        var knowledgeFilter = BuildKnowledgeQueryFilter(visibleFolderIds, request);
        var vectorResults = await vectorStore.QueryAsync(
            queryEmbedding,
            SearchLimit,
            knowledgeFilter,
            cancellationToken);
        var keywordResults = await keywordIndexService.SearchAsync(
            queryUnderstanding.Keywords,
            KeywordSearchLimit,
            knowledgeFilter,
            cancellationToken);
        var mergedCandidates = MergeCandidateResultsWithSources(vectorResults, keywordResults);
        var filterResult = await AnalyzeCandidateAccessAsync(
            mergedCandidates.Select(candidate => candidate.Result).ToList(),
            visibleFolderIds,
            request,
            cancellationToken);
        var finalContext = RerankAndPackContext(filterResult.Allowed, queryUnderstanding, request);
        var candidates = BuildCandidateResponses(mergedCandidates, filterResult, finalContext, queryUnderstanding, request);
        var finalContextKeys = finalContext.Select(GetResponseKey).ToArray();
        var finalCandidates = finalContextKeys
            .Select(key => candidates.FirstOrDefault(candidate => GetResponseKey(candidate) == key))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        return new RetrievalExplainResponse(
            question,
            request.ScopeType,
            new RetrievalQueryUnderstandingResponse(
                question,
                queryUnderstanding.NormalizedQuestion,
                queryUnderstanding.Keywords),
            new RetrievalFilterResponse(
                knowledgeFilter.SourceTypes.ToArray(),
                knowledgeFilter.Statuses.ToArray(),
                knowledgeFilter.IncludeCompanyVisible,
                visibleFolderIds.Count,
                knowledgeFilter.FolderIds.Count,
                knowledgeFilter.DocumentId),
            new RetrievalCandidateStatsResponse(
                vectorResults.Count,
                keywordResults.Count,
                mergedCandidates.Count,
                filterResult.Allowed.Count,
                finalContext.Count),
            finalCandidates,
            candidates);
    }

    private static QueryUnderstanding UnderstandQuery(string question)
    {
        var keywords = TokenizeForSearch(question)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();

        return new QueryUnderstanding(keywords, NormalizeForSearch(question));
    }

    private static IReadOnlyList<KnowledgeVectorSearchResult> MergeCandidateResults(
        IReadOnlyList<KnowledgeVectorSearchResult> vectorResults,
        IReadOnlyList<KnowledgeVectorSearchResult> keywordResults)
    {
        var merged = new List<KnowledgeVectorSearchResult>(vectorResults.Count + keywordResults.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in vectorResults.Concat(keywordResults))
        {
            var id = GetString(result.Metadata, "chunk_id") ?? result.Id;
            if (seenIds.Add(id))
            {
                merged.Add(result);
            }
        }

        return merged;
    }

    private static IReadOnlyList<CandidateSearchResult> MergeCandidateResultsWithSources(
        IReadOnlyList<KnowledgeVectorSearchResult> vectorResults,
        IReadOnlyList<KnowledgeVectorSearchResult> keywordResults)
    {
        var merged = new List<CandidateSearchResult>(vectorResults.Count + keywordResults.Count);
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in vectorResults)
        {
            var id = GetCandidateId(result);
            if (indexById.ContainsKey(id))
            {
                continue;
            }

            indexById[id] = merged.Count;
            merged.Add(new CandidateSearchResult(result, FromVector: true, FromKeyword: false));
        }

        foreach (var result in keywordResults)
        {
            var id = GetCandidateId(result);
            if (indexById.TryGetValue(id, out var existingIndex))
            {
                var existing = merged[existingIndex];
                merged[existingIndex] = existing with { FromKeyword = true };
                continue;
            }

            indexById[id] = merged.Count;
            merged.Add(new CandidateSearchResult(result, FromVector: false, FromKeyword: true));
        }

        return merged;
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
        var sourceTypes = new[] { "correction", "document", "wiki" };
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
        var filterResult = await AnalyzeCandidateAccessAsync(vectorResults, visibleFolderIds, request, cancellationToken);
        return filterResult.Allowed;
    }

    private async Task<ChunkFilterResult> AnalyzeCandidateAccessAsync(
        IReadOnlyList<KnowledgeVectorSearchResult> vectorResults,
        IReadOnlySet<Guid> visibleFolderIds,
        AskQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var candidates = new List<CandidateChunk>();
        var rejected = new List<RejectedKnowledgeChunk>();

        foreach (var result in vectorResults)
        {
            var chunk = ToRetrievedChunk(result);
            if (chunk is null)
            {
                rejected.Add(new RejectedKnowledgeChunk(result, null, "Rejected because required retrieval metadata is missing or invalid."));
                continue;
            }

            if (!IsInRequestedScope(chunk, request))
            {
                rejected.Add(new RejectedKnowledgeChunk(result, chunk, "Rejected because it is outside the requested scope."));
                continue;
            }

            if (!IsVisibleByFolder(chunk, visibleFolderIds))
            {
                rejected.Add(new RejectedKnowledgeChunk(result, chunk, "Rejected because its folder is not visible to the current user."));
                continue;
            }

            candidates.Add(new CandidateChunk(result, chunk));
        }

        var documentVersionIds = candidates
            .Where(candidate => candidate.Chunk.SourceType == KnowledgeSourceType.Document && candidate.Chunk.DocumentVersionId is not null)
            .Select(candidate => candidate.Chunk.DocumentVersionId!.Value)
            .ToHashSet();
        var wikiPageIds = candidates
            .Where(candidate => candidate.Chunk.SourceType == KnowledgeSourceType.Wiki && candidate.Chunk.WikiPageId is not null)
            .Select(candidate => candidate.Chunk.WikiPageId!.Value)
            .ToHashSet();
        var correctionIds = candidates
            .Where(candidate => candidate.Chunk.SourceType == KnowledgeSourceType.Correction)
            .Select(candidate => Guid.TryParse(candidate.Chunk.SourceId, out var correctionId) ? correctionId : (Guid?)null)
            .Where(correctionId => correctionId is not null)
            .Select(correctionId => correctionId!.Value)
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
        var visibleCorrections = await dbContext.KnowledgeCorrections
            .AsNoTracking()
            .Where(correction =>
                correctionIds.Contains(correction.Id) &&
                correction.Status == KnowledgeCorrectionStatus.Approved &&
                (
                    (correction.VisibilityScope == VisibilityScope.Company) ||
                    (correction.VisibilityScope == VisibilityScope.Folder && correction.FolderId != null && visibleFolderIds.Contains(correction.FolderId.Value))
                ))
            .Select(correction => new CorrectionVisibility(correction.Id, correction.VisibilityScope, correction.FolderId, correction.DocumentId))
            .ToDictionaryAsync(correction => correction.Id, cancellationToken);

        var allowed = new List<RetrievedKnowledgeChunk>();
        foreach (var candidate in candidates)
        {
            var chunk = candidate.Chunk;
            var isAllowed = chunk.SourceType switch
            {
                KnowledgeSourceType.Correction => IsAllowedCorrectionChunk(chunk, visibleCorrections, request),
                KnowledgeSourceType.Wiki => IsAllowedWikiChunk(chunk, visibleWikiPages, request),
                KnowledgeSourceType.Document => chunk.DocumentVersionId is not null && currentVersionIdSet.Contains(chunk.DocumentVersionId.Value),
                _ => false,
            };

            if (isAllowed)
            {
                allowed.Add(chunk);
                continue;
            }

            rejected.Add(new RejectedKnowledgeChunk(candidate.Result, chunk, GetSqlRejectionDecision(chunk)));
        }

        return new ChunkFilterResult(allowed, rejected);
    }

    private static List<RetrievedKnowledgeChunk> RerankAndPackContext(
        IReadOnlyList<RetrievedKnowledgeChunk> chunks,
        QueryUnderstanding queryUnderstanding,
        AskQuestionRequest request)
    {
        var ranked = chunks
            .Select((chunk, index) => new RankedKnowledgeChunk(
                chunk,
                index,
                ScoreChunk(chunk, queryUnderstanding, request)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.Chunk);

        var packed = new List<RetrievedKnowledgeChunk>();
        var seenSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in ranked)
        {
            var exactSourceKey = GetExactSourceKey(chunk);
            if (!seenSourceKeys.Add(exactSourceKey))
            {
                continue;
            }

            var packingKey = GetPackingKey(chunk);
            packedCounts.TryGetValue(packingKey, out var currentCount);
            if (currentCount >= MaxChunksPerKnowledgeItem)
            {
                continue;
            }

            packed.Add(chunk);
            packedCounts[packingKey] = currentCount + 1;

            if (packed.Count >= MaxContextChunks)
            {
                break;
            }
        }

        return packed;
    }

    private static IReadOnlyList<RetrievalCandidateResponse> BuildCandidateResponses(
        IReadOnlyList<CandidateSearchResult> mergedCandidates,
        ChunkFilterResult filterResult,
        IReadOnlyList<RetrievedKnowledgeChunk> finalContext,
        QueryUnderstanding queryUnderstanding,
        AskQuestionRequest request)
    {
        var rejectedById = filterResult.Rejected
            .GroupBy(rejected => GetCandidateId(rejected.Result), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var finalContextKeys = finalContext
            .Select(GetResponseKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return mergedCandidates
            .Select(candidate =>
            {
                var candidateId = GetCandidateId(candidate.Result);
                rejectedById.TryGetValue(candidateId, out var rejected);
                var chunk = rejected?.Chunk ?? ToRetrievedChunk(candidate.Result);
                var selected = chunk is not null && finalContextKeys.Contains(GetResponseKey(chunk));
                return ToRetrievalCandidateResponse(candidate, chunk, rejected, selected, queryUnderstanding, request);
            })
            .ToList();
    }

    private static RetrievalCandidateResponse ToRetrievalCandidateResponse(
        CandidateSearchResult candidate,
        RetrievedKnowledgeChunk? chunk,
        RejectedKnowledgeChunk? rejected,
        bool selected,
        QueryUnderstanding queryUnderstanding,
        AskQuestionRequest request)
    {
        var candidateId = GetCandidateId(candidate.Result);
        if (chunk is null)
        {
            return new RetrievalCandidateResponse(
                candidateId,
                GetRetrievalSource(candidate),
                GetString(candidate.Result.Metadata, "source_type") ?? "unknown",
                GetString(candidate.Result.Metadata, "source_id") ?? candidate.Result.Id,
                GetString(candidate.Result.Metadata, "title") ?? "Unknown source",
                GetString(candidate.Result.Metadata, "folder_path") ?? string.Empty,
                GetString(candidate.Result.Metadata, "section_title"),
                GetInt(candidate.Result.Metadata, "section_index"),
                candidate.Result.Distance,
                0,
                [],
                ["metadata unavailable"],
                PassedPermissionFilter: false,
                SelectedForContext: false,
                rejected?.Decision ?? "Rejected because required retrieval metadata is missing or invalid.",
                ToExcerpt(candidate.Result.Text));
        }

        var score = ExplainChunkScore(chunk, queryUnderstanding, request);
        var passedPermissionFilter = rejected is null;
        var decision = rejected?.Decision
            ?? (selected
                ? "Selected for final context after rerank and context packing."
                : "Allowed by permission filter but not selected after rerank/context packing.");

        return new RetrievalCandidateResponse(
            candidateId,
            GetRetrievalSource(candidate),
            chunk.SourceType.ToString(),
            chunk.SourceId,
            chunk.Title,
            chunk.FolderPath,
            chunk.SectionTitle,
            chunk.SectionIndex,
            chunk.Distance,
            score.Score,
            score.MatchedKeywords,
            score.Reasons,
            passedPermissionFilter,
            selected,
            decision,
            ToExcerpt(chunk.Text));
    }

    private static string GetRetrievalSource(CandidateSearchResult candidate)
    {
        return (candidate.FromVector, candidate.FromKeyword) switch
        {
            (true, true) => "Vector+Keyword",
            (true, false) => "Vector",
            (false, true) => "Keyword",
            _ => "Unknown",
        };
    }

    private static double ScoreChunk(
        RetrievedKnowledgeChunk chunk,
        QueryUnderstanding queryUnderstanding,
        AskQuestionRequest request)
    {
        return ExplainChunkScore(chunk, queryUnderstanding, request).Score;
    }

    private static ChunkScoreExplanation ExplainChunkScore(
        RetrievedKnowledgeChunk chunk,
        QueryUnderstanding queryUnderstanding,
        AskQuestionRequest request)
    {
        var normalizedText = NormalizeForSearch($"{chunk.Title} {chunk.SectionTitle} {chunk.Text}");
        var matchedKeywords = queryUnderstanding.Keywords
            .Where(keyword => normalizedText.Contains(keyword, StringComparison.Ordinal))
            .ToArray();
        var allKeywordsMatched = queryUnderstanding.Keywords.Length > 0 && matchedKeywords.Length == queryUnderstanding.Keywords.Length;
        var phraseMatch = !string.IsNullOrWhiteSpace(queryUnderstanding.NormalizedQuestion)
            && normalizedText.Contains(queryUnderstanding.NormalizedQuestion, StringComparison.Ordinal);
        var distanceScore = chunk.Distance is null
            ? 0
            : Math.Max(0, 1 - Math.Min(chunk.Distance.Value, 1));
        var sourcePriority = SourcePriority(chunk.SourceType);
        var keywordScore = matchedKeywords.Length * 12;
        var allKeywordScore = allKeywordsMatched ? 20 : 0;
        var phraseScore = phraseMatch ? 18 : 0;
        var scopeScore = ScopePriority(chunk, request);
        var reasons = new List<string> { $"source priority {chunk.SourceType} +{sourcePriority}" };

        if (matchedKeywords.Length > 0)
        {
            reasons.Add($"matched keywords {string.Join(", ", matchedKeywords)} +{keywordScore}");
        }

        if (allKeywordScore > 0)
        {
            reasons.Add($"all query keywords matched +{allKeywordScore}");
        }

        if (phraseScore > 0)
        {
            reasons.Add($"full phrase match +{phraseScore}");
        }

        if (scopeScore > 0)
        {
            reasons.Add($"requested scope match +{scopeScore}");
        }

        if (distanceScore > 0)
        {
            reasons.Add($"vector distance score +{distanceScore:0.###}");
        }

        return new ChunkScoreExplanation(
            sourcePriority + keywordScore + allKeywordScore + phraseScore + scopeScore + distanceScore,
            matchedKeywords,
            reasons);
    }

    private static int SourcePriority(KnowledgeSourceType sourceType)
    {
        return sourceType switch
        {
            KnowledgeSourceType.Correction => 100,
            KnowledgeSourceType.Wiki => 45,
            KnowledgeSourceType.Document => 20,
            _ => 0,
        };
    }

    private static int ScopePriority(RetrievedKnowledgeChunk chunk, AskQuestionRequest request)
    {
        return request.ScopeType switch
        {
            AiScopeType.Folder when chunk.FolderId == request.FolderId => 20,
            AiScopeType.Document when chunk.DocumentId == request.DocumentId => 20,
            _ => 0,
        };
    }

    private static string GetExactSourceKey(RetrievedKnowledgeChunk chunk)
    {
        return $"{chunk.SourceType}:{chunk.SourceId}:{chunk.SectionIndex}:{NormalizeForSearch(chunk.SectionTitle ?? string.Empty)}";
    }

    private static string GetPackingKey(RetrievedKnowledgeChunk chunk)
    {
        if (chunk.DocumentId is not null)
        {
            return $"document:{chunk.DocumentId}";
        }

        if (chunk.WikiPageId is not null)
        {
            return $"wiki:{chunk.WikiPageId}";
        }

        return $"{chunk.SourceType}:{chunk.SourceId}";
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

    private static bool IsAllowedCorrectionChunk(
        RetrievedKnowledgeChunk chunk,
        IReadOnlyDictionary<Guid, CorrectionVisibility> visibleCorrections,
        AskQuestionRequest request)
    {
        if (!Guid.TryParse(chunk.SourceId, out var correctionId)
            || !visibleCorrections.TryGetValue(correctionId, out var correction))
        {
            return false;
        }

        if (request.ScopeType == AiScopeType.Folder && correction.FolderId != request.FolderId)
        {
            return false;
        }

        if (request.ScopeType == AiScopeType.Document && correction.DocumentId != request.DocumentId)
        {
            return false;
        }

        return true;
    }

    private static string GetSqlRejectionDecision(RetrievedKnowledgeChunk chunk)
    {
        return chunk.SourceType switch
        {
            KnowledgeSourceType.Correction => "Rejected because the correction is not approved, not visible, or outside the requested scope.",
            KnowledgeSourceType.Wiki => "Rejected because the wiki page is archived, not visible, or outside the requested scope.",
            KnowledgeSourceType.Document => "Rejected because the document chunk is not the current indexed version, was deleted, or is outside visible folders.",
            _ => "Rejected because the source type is not supported by retrieval.",
        };
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

    private static string GetCandidateId(KnowledgeVectorSearchResult result)
    {
        return GetString(result.Metadata, "chunk_id") ?? result.Id;
    }

    private static string GetResponseKey(RetrievedKnowledgeChunk chunk)
    {
        return $"{chunk.SourceType}:{chunk.SourceId}:{chunk.SectionIndex}:{NormalizeForSearch(chunk.SectionTitle ?? string.Empty)}";
    }

    private static string GetResponseKey(RetrievalCandidateResponse candidate)
    {
        return $"{candidate.SourceType}:{candidate.SourceId}:{candidate.SectionIndex}:{NormalizeForSearch(candidate.SectionTitle ?? string.Empty)}";
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
        if (string.Equals(chunk.VisibilityScope, "company", StringComparison.OrdinalIgnoreCase))
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

    private static string[] TokenizeForSearch(string text)
    {
        return NormalizeForSearch(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .ToArray();
    }

    private static string NormalizeForSearch(string text)
    {
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

    private sealed record QueryUnderstanding(string[] Keywords, string NormalizedQuestion);

    private sealed record RankedKnowledgeChunk(RetrievedKnowledgeChunk Chunk, int OriginalIndex, double Score);

    private sealed record CandidateSearchResult(KnowledgeVectorSearchResult Result, bool FromVector, bool FromKeyword);

    private sealed record CandidateChunk(KnowledgeVectorSearchResult Result, RetrievedKnowledgeChunk Chunk);

    private sealed record ChunkFilterResult(List<RetrievedKnowledgeChunk> Allowed, List<RejectedKnowledgeChunk> Rejected);

    private sealed record RejectedKnowledgeChunk(KnowledgeVectorSearchResult Result, RetrievedKnowledgeChunk? Chunk, string Decision);

    private sealed record ChunkScoreExplanation(double Score, IReadOnlyList<string> MatchedKeywords, IReadOnlyList<string> Reasons);

    private sealed record WikiPageVisibility(Guid Id, Guid SourceDocumentId, VisibilityScope VisibilityScope, Guid? FolderId);

    private sealed record CorrectionVisibility(Guid Id, VisibilityScope VisibilityScope, Guid? FolderId, Guid? DocumentId);
}
