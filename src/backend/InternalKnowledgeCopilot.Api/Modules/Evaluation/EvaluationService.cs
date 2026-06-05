using System.Globalization;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Evaluation;

public interface IEvaluationService
{
    Task<IReadOnlyList<EvaluationCaseResponse>> GetCasesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default);

    Task<EvaluationCaseResponse> CreateCaseFromFeedbackAsync(Guid feedbackId, Guid reviewerId, CreateEvaluationCaseFromFeedbackRequest request, CancellationToken cancellationToken = default);

    Task<EvaluationCaseResponse> CreateCrossTenantLeakageCaseAsync(Guid reviewerId, CreateCrossTenantLeakageCaseRequest request, CancellationToken cancellationToken = default);

    Task<EvaluationRunResponse> RunAsync(Guid reviewerId, RunEvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed class EvaluationService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAiQuestionService aiQuestionService,
    IAuditLogService auditLogService,
    IAiTaskRouter? aiTaskRouter = null,
    ILogger<EvaluationService>? logger = null) : IEvaluationService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were", "has", "have", "not",
        "mot", "cac", "cua", "cho", "voi", "khi", "thi", "la", "va", "de", "duoc", "trong", "ngoai",
        "neu", "sau", "truoc", "phai", "can", "nen", "hoi", "tra", "loi", "nguoi", "dung",
    };

    public async Task<IReadOnlyList<EvaluationCaseResponse>> GetCasesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var cases = await dbContext.EvaluationCases
            .AsNoTracking()
            .Where(evaluationCase => evaluationCase.TenantId == tenantId)
            .OrderByDescending(evaluationCase => evaluationCase.CreatedAt)
            .ToListAsync(cancellationToken);

        return cases.Select(ToCaseResponse).ToList();
    }

    public async Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var runs = await dbContext.EvaluationRuns
            .AsNoTracking()
            .Include(run => run.Results)
            .ThenInclude(result => result.EvaluationCase)
            .Where(run => run.TenantId == tenantId)
            .OrderByDescending(run => run.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return runs.Select(ToRunResponse).ToList();
    }

    public async Task<EvaluationCaseResponse> CreateCaseFromFeedbackAsync(
        Guid feedbackId,
        Guid reviewerId,
        CreateEvaluationCaseFromFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var feedback = await dbContext.AiFeedback
            .Include(item => item.AiInteraction)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == feedbackId, cancellationToken);

        if (feedback?.AiInteraction is null)
        {
            throw new KeyNotFoundException("feedback_not_found");
        }

        if (feedback.Value != AiFeedbackValue.Incorrect)
        {
            throw new InvalidOperationException("feedback_must_be_incorrect");
        }

        var expectedAnswer = request.ExpectedAnswer?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedAnswer))
        {
            throw new ArgumentException("expected_answer_required");
        }

        var scopeType = request.ScopeType ?? feedback.AiInteraction.ScopeType;
        var folderId = scopeType == AiScopeType.Folder
            ? request.FolderId ?? feedback.AiInteraction.ScopeFolderId
            : null;
        var documentId = scopeType == AiScopeType.Document
            ? request.DocumentId ?? feedback.AiInteraction.ScopeDocumentId
            : null;

        ValidateScope(scopeType, folderId, documentId);

        var keywords = NormalizeKeywords(request.ExpectedKeywords, expectedAnswer);
        var forbiddenKeywords = NormalizeOptionalKeywords(request.ForbiddenKeywords);
        var now = DateTimeOffset.UtcNow;
        var evaluationCase = new EvaluationCaseEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceFeedbackId = feedback.Id,
            Question = feedback.AiInteraction.Question,
            ExpectedAnswer = expectedAnswer,
            ExpectedKeywordsJson = JsonSerializer.Serialize(keywords),
            ForbiddenKeywordsJson = JsonSerializer.Serialize(forbiddenKeywords),
            CaseKind = request.CaseKind,
            ScopeType = scopeType,
            FolderId = folderId,
            DocumentId = documentId,
            ApplicationId = request.ApplicationId,
            KnowledgeSourceId = request.KnowledgeSourceId,
            ExternalObjectType = CleanOptional(request.ExternalObjectType, 100),
            ExternalObjectId = CleanOptional(request.ExternalObjectId, 300),
            CreatedByUserId = reviewerId,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.EvaluationCases.Add(evaluationCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            reviewerId,
            "EvaluationCaseCreated",
            "EvaluationCase",
            evaluationCase.Id,
            new { feedbackId },
            cancellationToken);

        return ToCaseResponse(evaluationCase);
    }

    public async Task<EvaluationCaseResponse> CreateCrossTenantLeakageCaseAsync(
        Guid reviewerId,
        CreateCrossTenantLeakageCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("question_required");
        }

        var forbiddenKeywords = NormalizeOptionalKeywords(request.ForbiddenKeywords);
        if (forbiddenKeywords.Count == 0)
        {
            throw new ArgumentException("forbidden_keywords_required");
        }

        ValidateScope(request.ScopeType, request.FolderId, request.DocumentId);
        var expectedAnswer = string.IsNullOrWhiteSpace(request.ExpectedAnswer)
            ? "The answer must not include forbidden cross-tenant facts."
            : request.ExpectedAnswer.Trim();
        var keywords = NormalizeOptionalKeywords(request.ExpectedKeywords);
        var now = DateTimeOffset.UtcNow;
        var evaluationCase = new EvaluationCaseEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Question = question,
            ExpectedAnswer = expectedAnswer,
            ExpectedKeywordsJson = JsonSerializer.Serialize(keywords),
            ForbiddenKeywordsJson = JsonSerializer.Serialize(forbiddenKeywords),
            CaseKind = EvaluationCaseKind.CrossTenantLeakage,
            ScopeType = request.ScopeType,
            FolderId = request.ScopeType == AiScopeType.Folder ? request.FolderId : null,
            DocumentId = request.ScopeType == AiScopeType.Document ? request.DocumentId : null,
            ApplicationId = request.ApplicationId,
            KnowledgeSourceId = request.KnowledgeSourceId,
            ExternalObjectType = CleanOptional(request.ExternalObjectType, 100),
            ExternalObjectId = CleanOptional(request.ExternalObjectId, 300),
            CreatedByUserId = reviewerId,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.EvaluationCases.Add(evaluationCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            reviewerId,
            "CrossTenantLeakageEvaluationCaseCreated",
            "EvaluationCase",
            evaluationCase.Id,
            new { ForbiddenKeywordCount = forbiddenKeywords.Count, request.ApplicationId, request.KnowledgeSourceId },
            cancellationToken);

        return ToCaseResponse(evaluationCase);
    }

    public async Task<EvaluationRunResponse> RunAsync(Guid reviewerId, RunEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var casesQuery = dbContext.EvaluationCases
            .AsNoTracking()
            .Where(evaluationCase => evaluationCase.TenantId == tenantId && evaluationCase.IsActive);

        if (request.CaseId is not null)
        {
            casesQuery = casesQuery.Where(evaluationCase => evaluationCase.Id == request.CaseId);
        }

        var cases = await casesQuery
            .OrderBy(evaluationCase => evaluationCase.CreatedAt)
            .ToListAsync(cancellationToken);

        if (cases.Count == 0 && request.CaseId is not null)
        {
            throw new KeyNotFoundException("evaluation_case_not_found");
        }

        if (cases.Count == 0)
        {
            throw new InvalidOperationException("evaluation_cases_required");
        }

        var now = DateTimeOffset.UtcNow;
        var taskRoute = aiTaskRouter is null
            ? null
            : await aiTaskRouter.ResolveAsync(AiTaskType.Evaluation, cancellationToken);
        var runId = Guid.NewGuid();
        var resultEntities = new List<EvaluationRunResultEntity>();

        foreach (var evaluationCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resultEntities.Add(await RunCaseAsync(tenantId, runId, reviewerId, evaluationCase, cancellationToken));
        }

        var passedCases = resultEntities.Count(result => result.Passed);
        var leakageCases = cases.Count(item => item.CaseKind == EvaluationCaseKind.CrossTenantLeakage);
        var leakageFailures = resultEntities.Count(result =>
            !result.Passed &&
            cases.Any(item => item.Id == result.EvaluationCaseId && item.CaseKind == EvaluationCaseKind.CrossTenantLeakage));
        var run = new EvaluationRunEntity
        {
            Id = runId,
            TenantId = tenantId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            TotalCases = resultEntities.Count,
            PassedCases = passedCases,
            FailedCases = resultEntities.Count - passedCases,
            CrossTenantLeakageCases = leakageCases,
            CrossTenantLeakageFailures = leakageFailures,
            AiMetadataJson = taskRoute is null ? null : JsonSerializer.Serialize(new
            {
                taskRoute.TaskType,
                taskRoute.ProviderName,
                taskRoute.Model,
                taskRoute.PromptTemplateId,
                taskRoute.PromptTemplateVersion,
                taskRoute.PromptHash
            }),
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            FinishedAt = DateTimeOffset.UtcNow,
            Results = resultEntities,
        };

        dbContext.EvaluationRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            reviewerId,
            "EvaluationRunCompleted",
            "EvaluationRun",
            run.Id,
            new { run.TotalCases, run.PassedCases, run.FailedCases, run.CrossTenantLeakageCases, run.CrossTenantLeakageFailures },
            cancellationToken);
        logger?.LogInformation(
            "Evaluation run {EvaluationRunId} completed for tenant {TenantId}: {PassedCases}/{TotalCases} passed, leakage failures {LeakageFailures}/{LeakageCases}.",
            run.Id,
            tenantId,
            run.PassedCases,
            run.TotalCases,
            run.CrossTenantLeakageFailures,
            run.CrossTenantLeakageCases);

        var savedRun = await dbContext.EvaluationRuns
            .AsNoTracking()
            .Include(item => item.Results)
            .ThenInclude(result => result.EvaluationCase)
            .FirstAsync(item => item.TenantId == tenantId && item.Id == run.Id, cancellationToken);

        return ToRunResponse(savedRun);
    }

    private async Task<EvaluationRunResultEntity> RunCaseAsync(
        Guid tenantId,
        Guid runId,
        Guid reviewerId,
        EvaluationCaseEntity evaluationCase,
        CancellationToken cancellationToken)
    {
        var createdAt = DateTimeOffset.UtcNow;

        try
        {
            var response = await aiQuestionService.AskAsync(
                reviewerId,
                new AskQuestionRequest(
                    evaluationCase.Question,
                    evaluationCase.ScopeType,
                    evaluationCase.FolderId,
                    evaluationCase.DocumentId,
                    evaluationCase.ApplicationId,
                    evaluationCase.KnowledgeSourceId,
                    evaluationCase.ExternalObjectType,
                    evaluationCase.ExternalObjectId),
                cancellationToken);
            var expectedKeywords = DeserializeKeywords(evaluationCase.ExpectedKeywordsJson);
            var forbiddenKeywords = DeserializeKeywords(evaluationCase.ForbiddenKeywordsJson);
            var assessment = Assess(response, expectedKeywords, forbiddenKeywords, evaluationCase.CaseKind);

            return new EvaluationRunResultEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EvaluationRunId = runId,
                EvaluationCaseId = evaluationCase.Id,
                AiInteractionId = response.InteractionId,
                ActualAnswer = response.Answer,
                Passed = assessment.Passed,
                Score = assessment.Score,
                FailureReason = assessment.FailureReason,
                CreatedAt = createdAt,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new EvaluationRunResultEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EvaluationRunId = runId,
                EvaluationCaseId = evaluationCase.Id,
                ActualAnswer = string.Empty,
                Passed = false,
                Score = 0,
                FailureReason = $"run_error: {ex.Message}",
                CreatedAt = createdAt,
            };
        }
    }

    private static EvaluationAssessment Assess(
        AskQuestionResponse response,
        IReadOnlyList<string> expectedKeywords,
        IReadOnlyList<string> forbiddenKeywords,
        EvaluationCaseKind caseKind)
    {
        var normalizedAnswer = NormalizeForSearch(response.Answer);
        var forbiddenMatches = forbiddenKeywords
            .Select(keyword => new KeywordMatch(keyword, NormalizeForSearch(keyword)))
            .Where(match => !string.IsNullOrWhiteSpace(match.Normalized) && normalizedAnswer.Contains(match.Normalized, StringComparison.Ordinal))
            .Select(match => match.Original)
            .ToList();
        if (forbiddenMatches.Count > 0)
        {
            return new EvaluationAssessment(false, 0, $"forbidden_keywords_present: {string.Join(", ", forbiddenMatches)}");
        }

        if (caseKind == EvaluationCaseKind.CrossTenantLeakage && expectedKeywords.Count == 0)
        {
            return new EvaluationAssessment(true, 1, null);
        }

        var normalizedKeywords = expectedKeywords
            .Select(keyword => new KeywordMatch(keyword, NormalizeForSearch(keyword)))
            .Where(match => !string.IsNullOrWhiteSpace(match.Normalized))
            .ToList();

        if (normalizedKeywords.Count == 0)
        {
            return new EvaluationAssessment(false, 0, "expected_keywords_required");
        }

        var missingKeywords = normalizedKeywords
            .Where(match => !normalizedAnswer.Contains(match.Normalized, StringComparison.Ordinal))
            .Select(match => match.Original)
            .ToList();

        var score = (double)(normalizedKeywords.Count - missingKeywords.Count) / normalizedKeywords.Count;
        if (response.NeedsClarification)
        {
            return new EvaluationAssessment(false, Math.Round(score, 4), "ai_requested_clarification");
        }

        if (missingKeywords.Count > 0)
        {
            return new EvaluationAssessment(false, Math.Round(score, 4), $"missing_keywords: {string.Join(", ", missingKeywords)}");
        }

        return new EvaluationAssessment(true, 1, null);
    }

    private static void ValidateScope(AiScopeType scopeType, Guid? folderId, Guid? documentId)
    {
        if (scopeType == AiScopeType.Folder && folderId is null)
        {
            throw new ArgumentException("folder_required");
        }

        if (scopeType == AiScopeType.Document && documentId is null)
        {
            throw new ArgumentException("document_required");
        }
    }

    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string>? expectedKeywords, string expectedAnswer)
    {
        var keywords = (expectedKeywords ?? [])
            .Select(keyword => keyword.Trim())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return keywords.Count > 0 ? keywords : ExtractSignificantKeywords(expectedAnswer);
    }

    private static IReadOnlyList<string> NormalizeOptionalKeywords(IReadOnlyList<string>? keywords)
    {
        return (keywords ?? [])
            .Select(keyword => keyword.Trim())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static IReadOnlyList<string> ExtractSignificantKeywords(string text)
    {
        return NormalizeForSearch(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3 && !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeKeywords(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
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

    private static EvaluationCaseResponse ToCaseResponse(EvaluationCaseEntity evaluationCase)
    {
        return new EvaluationCaseResponse(
            evaluationCase.Id,
            evaluationCase.SourceFeedbackId,
            evaluationCase.Question,
            evaluationCase.ExpectedAnswer,
            DeserializeKeywords(evaluationCase.ExpectedKeywordsJson),
            DeserializeKeywords(evaluationCase.ForbiddenKeywordsJson),
            evaluationCase.CaseKind,
            evaluationCase.ScopeType,
            evaluationCase.FolderId,
            evaluationCase.DocumentId,
            evaluationCase.ApplicationId,
            evaluationCase.KnowledgeSourceId,
            evaluationCase.ExternalObjectType,
            evaluationCase.ExternalObjectId,
            evaluationCase.IsActive,
            evaluationCase.CreatedAt,
            evaluationCase.UpdatedAt);
    }

    private static EvaluationRunResponse ToRunResponse(EvaluationRunEntity run)
    {
        var passRate = run.TotalCases == 0 ? 0 : Math.Round((double)run.PassedCases / run.TotalCases * 100, 2);
        var results = run.Results
            .OrderBy(result => result.CreatedAt)
            .Select(result => new EvaluationRunResultResponse(
                result.Id,
                result.EvaluationCaseId,
                result.AiInteractionId,
                result.EvaluationCase?.Question ?? string.Empty,
                result.ActualAnswer,
                result.Passed,
                result.Score,
                result.FailureReason,
                result.CreatedAt))
            .ToList();

        return new EvaluationRunResponse(
            run.Id,
            run.Name,
            run.TotalCases,
            run.PassedCases,
            run.FailedCases,
            run.CrossTenantLeakageCases,
            run.CrossTenantLeakageFailures,
            passRate,
            run.CreatedAt,
            run.FinishedAt,
            results);
    }

    private sealed record EvaluationAssessment(bool Passed, double Score, string? FailureReason);

    private sealed record KeywordMatch(string Original, string Normalized);
}
