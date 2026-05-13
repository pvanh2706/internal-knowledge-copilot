using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Ai;

public sealed record AskQuestionRequest(
    string Question,
    AiScopeType ScopeType,
    Guid? FolderId,
    Guid? DocumentId);

public sealed record AskQuestionResponse(
    Guid InteractionId,
    string Answer,
    bool NeedsClarification,
    string Confidence,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> SuggestedFollowUps,
    IReadOnlyList<AiCitationResponse> Citations);

public sealed record AiCitationResponse(
    KnowledgeSourceType SourceType,
    string Title,
    string FolderPath,
    string? SectionTitle,
    string Excerpt);

public sealed record RetrievedKnowledgeChunk(
    KnowledgeSourceType SourceType,
    string SourceId,
    Guid? DocumentId,
    Guid? DocumentVersionId,
    Guid? WikiPageId,
    Guid? FolderId,
    string? VisibilityScope,
    string Title,
    string FolderPath,
    string? SectionTitle,
    int? SectionIndex,
    string Text,
    double? Distance);

public sealed record RetrievalExplainResponse(
    string Question,
    AiScopeType ScopeType,
    RetrievalQueryUnderstandingResponse QueryUnderstanding,
    RetrievalFilterResponse Filter,
    RetrievalCandidateStatsResponse CandidateStats,
    IReadOnlyList<RetrievalCandidateResponse> FinalContext,
    IReadOnlyList<RetrievalCandidateResponse> Candidates);

public sealed record RetrievalQueryUnderstandingResponse(
    string RewrittenQuestion,
    string NormalizedQuestion,
    IReadOnlyList<string> Keywords);

public sealed record RetrievalFilterResponse(
    IReadOnlyList<string> SourceTypes,
    IReadOnlyList<string> Statuses,
    bool IncludeCompanyVisible,
    int VisibleFolderCount,
    int FilteredFolderCount,
    Guid? DocumentId);

public sealed record RetrievalCandidateStatsResponse(
    int VectorCandidateCount,
    int KeywordCandidateCount,
    int MergedCandidateCount,
    int AllowedCandidateCount,
    int FinalContextCount);

public sealed record RetrievalCandidateResponse(
    string CandidateId,
    string RetrievalSource,
    string SourceType,
    string SourceId,
    string Title,
    string FolderPath,
    string? SectionTitle,
    int? SectionIndex,
    double? Distance,
    double Score,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> ScoreReasons,
    bool PassedPermissionFilter,
    bool SelectedForContext,
    string Decision,
    string Excerpt);
