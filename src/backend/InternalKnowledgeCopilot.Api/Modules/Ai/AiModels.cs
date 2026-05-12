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
