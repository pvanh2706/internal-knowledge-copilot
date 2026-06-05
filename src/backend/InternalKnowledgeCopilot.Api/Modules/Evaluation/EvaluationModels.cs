using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Evaluation;

public sealed record CreateEvaluationCaseFromFeedbackRequest(
    string ExpectedAnswer,
    IReadOnlyList<string>? ExpectedKeywords,
    AiScopeType? ScopeType,
    Guid? FolderId,
    Guid? DocumentId,
    bool IsActive = true,
    EvaluationCaseKind CaseKind = EvaluationCaseKind.Regression,
    IReadOnlyList<string>? ForbiddenKeywords = null,
    Guid? ApplicationId = null,
    Guid? KnowledgeSourceId = null,
    string? ExternalObjectType = null,
    string? ExternalObjectId = null);

public sealed record CreateCrossTenantLeakageCaseRequest(
    string Question,
    IReadOnlyList<string> ForbiddenKeywords,
    string? ExpectedAnswer = null,
    IReadOnlyList<string>? ExpectedKeywords = null,
    AiScopeType ScopeType = AiScopeType.All,
    Guid? FolderId = null,
    Guid? DocumentId = null,
    Guid? ApplicationId = null,
    Guid? KnowledgeSourceId = null,
    string? ExternalObjectType = null,
    string? ExternalObjectId = null,
    bool IsActive = true);

public sealed record RunEvaluationRequest(Guid? CaseId, string? Name);

public sealed record EvaluationCaseResponse(
    Guid Id,
    Guid? SourceFeedbackId,
    string Question,
    string ExpectedAnswer,
    IReadOnlyList<string> ExpectedKeywords,
    IReadOnlyList<string> ForbiddenKeywords,
    EvaluationCaseKind CaseKind,
    AiScopeType ScopeType,
    Guid? FolderId,
    Guid? DocumentId,
    Guid? ApplicationId,
    Guid? KnowledgeSourceId,
    string? ExternalObjectType,
    string? ExternalObjectId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationRunResponse(
    Guid Id,
    string? Name,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    int CrossTenantLeakageCases,
    int CrossTenantLeakageFailures,
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
