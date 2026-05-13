using System.Globalization;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Evaluation;

public interface IEvaluationService
{
    Task<IReadOnlyList<EvaluationCaseResponse>> GetCasesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default);

    Task<EvaluationCaseResponse> CreateCaseFromFeedbackAsync(Guid feedbackId, Guid reviewerId, CreateEvaluationCaseFromFeedbackRequest request, CancellationToken cancellationToken = default);

    Task<EvaluationRunResponse> RunAsync(Guid reviewerId, RunEvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed class EvaluationService(
    AppDbContext dbContext,
    IAiQuestionService aiQuestionService,
    IAuditLogService auditLogService) : IEvaluationService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were", "has", "have", "not",
        "mot", "cac", "cua", "cho", "voi", "khi", "thi", "la", "va", "de", "duoc", "trong", "ngoai",
        "neu", "sau", "truoc", "phai", "can", "nen", "hoi", "tra", "loi", "nguoi", "dung",
    };

    public async Task<IReadOnlyList<EvaluationCaseResponse>> GetCasesAsync(CancellationToken cancellationToken = default)
    {
        var cases = await dbContext.EvaluationCases
            .AsNoTracking()
            .OrderByDescending(evaluationCase => evaluationCase.CreatedAt)
            .ToListAsync(cancellationToken);

        return cases.Select(ToCaseResponse).ToList();
    }

    public async Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default)
    {
        var runs = await dbContext.EvaluationRuns
            .AsNoTracking()
            .Include(run => run.Results)
            .ThenInclude(result => result.EvaluationCase)
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
        var feedback = await dbContext.AiFeedback
            .Include(item => item.AiInteraction)
            .FirstOrDefaultAsync(item => item.Id == feedbackId, cancellationToken);

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
        var now = DateTimeOffset.UtcNow;
        var evaluationCase = new EvaluationCaseEntity
        {
            Id = Guid.NewGuid(),
            SourceFeedbackId = feedback.Id,
            Question = feedback.AiInteraction.Question,
            ExpectedAnswer = expectedAnswer,
            ExpectedKeywordsJson = JsonSerializer.Serialize(keywords),
            ScopeType = scopeType,
            FolderId = folderId,
            DocumentId = documentId,
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

    public async Task<EvaluationRunResponse> RunAsync(Guid reviewerId, RunEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var casesQuery = dbContext.EvaluationCases
            .AsNoTracking()
            .Where(evaluationCase => evaluationCase.IsActive);

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
        var runId = Guid.NewGuid();
        var resultEntities = new List<EvaluationRunResultEntity>();

        foreach (var evaluationCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resultEntities.Add(await RunCaseAsync(runId, reviewerId, evaluationCase, cancellationToken));
        }

        var passedCases = resultEntities.Count(result => result.Passed);
        var run = new EvaluationRunEntity
        {
            Id = runId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            TotalCases = resultEntities.Count,
            PassedCases = passedCases,
            FailedCases = resultEntities.Count - passedCases,
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
            new { run.TotalCases, run.PassedCases, run.FailedCases },
            cancellationToken);

        var savedRun = await dbContext.EvaluationRuns
            .AsNoTracking()
            .Include(item => item.Results)
            .ThenInclude(result => result.EvaluationCase)
            .FirstAsync(item => item.Id == run.Id, cancellationToken);

        return ToRunResponse(savedRun);
    }

    private async Task<EvaluationRunResultEntity> RunCaseAsync(
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
                    evaluationCase.DocumentId),
                cancellationToken);
            var expectedKeywords = DeserializeKeywords(evaluationCase.ExpectedKeywordsJson);
            var assessment = Assess(response, expectedKeywords);

            return new EvaluationRunResultEntity
            {
                Id = Guid.NewGuid(),
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

    private static EvaluationAssessment Assess(AskQuestionResponse response, IReadOnlyList<string> expectedKeywords)
    {
        var normalizedAnswer = NormalizeForSearch(response.Answer);
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
            evaluationCase.ScopeType,
            evaluationCase.FolderId,
            evaluationCase.DocumentId,
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
            passRate,
            run.CreatedAt,
            run.FinishedAt,
            results);
    }

    private sealed record EvaluationAssessment(bool Passed, double Score, string? FailureReason);

    private sealed record KeywordMatch(string Original, string Normalized);
}
