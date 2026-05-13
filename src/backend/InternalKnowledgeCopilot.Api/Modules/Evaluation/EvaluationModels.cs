using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Evaluation;

public sealed record CreateEvaluationCaseFromFeedbackRequest(
    string ExpectedAnswer,
    IReadOnlyList<string>? ExpectedKeywords,
    AiScopeType? ScopeType,
    Guid? FolderId,
    Guid? DocumentId,
    bool IsActive = true);

public sealed record RunEvaluationRequest(Guid? CaseId, string? Name);

public sealed record EvaluationCaseResponse(
    Guid Id,
    Guid? SourceFeedbackId,
    string Question,
    string ExpectedAnswer,
    IReadOnlyList<string> ExpectedKeywords,
    AiScopeType ScopeType,
    Guid? FolderId,
    Guid? DocumentId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationRunResponse(
    Guid Id,
    string? Name,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    double PassRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<EvaluationRunResultResponse> Results);

public sealed record EvaluationRunResultResponse(
    Guid Id,
    Guid EvaluationCaseId,
    Guid? AiInteractionId,
    string Question,
    string ActualAnswer,
    bool Passed,
    double Score,
    string? FailureReason,
    DateTimeOffset CreatedAt);
