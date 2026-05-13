using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class DashboardController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? teamId,
        [FromQuery] Guid? folderId,
        CancellationToken cancellationToken)
    {
        var folderScopeQuery = dbContext.Folders
            .AsNoTracking()
            .Where(folder => folder.DeletedAt == null);

        if (folderId is not null)
        {
            folderScopeQuery = folderScopeQuery.Where(folder => folder.Id == folderId);
        }

        if (teamId is not null)
        {
            var teamFolderIds = dbContext.FolderPermissions
                .AsNoTracking()
                .Where(permission => permission.TeamId == teamId && permission.CanView)
                .Select(permission => permission.FolderId);

            folderScopeQuery = folderScopeQuery.Where(folder => teamFolderIds.Contains(folder.Id));
        }

        var scopedFolderIds = folderScopeQuery.Select(folder => folder.Id);

        var documentsQuery = dbContext.Documents
            .AsNoTracking()
            .Where(document => document.DeletedAt == null && scopedFolderIds.Contains(document.FolderId));

        if (from is not null)
        {
            documentsQuery = documentsQuery.Where(document => document.UpdatedAt >= from);
        }

        if (to is not null)
        {
            documentsQuery = documentsQuery.Where(document => document.UpdatedAt <= to);
        }

        var documentCounts = await documentsQuery
            .GroupBy(document => document.Status)
            .Select(group => new NamedCountResponse(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        var wikiQuery = dbContext.WikiDrafts
            .AsNoTracking()
            .Where(draft => scopedFolderIds.Contains(draft.SourceDocument!.FolderId));

        if (from is not null)
        {
            wikiQuery = wikiQuery.Where(draft => draft.UpdatedAt >= from);
        }

        if (to is not null)
        {
            wikiQuery = wikiQuery.Where(draft => draft.UpdatedAt <= to);
        }

        var wikiCounts = await wikiQuery
            .GroupBy(draft => draft.Status)
            .Select(group => new NamedCountResponse(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        var aiQuery = dbContext.AiInteractions.AsNoTracking();
        if (from is not null)
        {
            aiQuery = aiQuery.Where(interaction => interaction.CreatedAt >= from);
        }

        if (to is not null)
        {
            aiQuery = aiQuery.Where(interaction => interaction.CreatedAt <= to);
        }

        var aiQuestionCount = await aiQuery.CountAsync(cancellationToken);

        var feedbackQuery = dbContext.AiFeedback.AsNoTracking();
        if (from is not null)
        {
            feedbackQuery = feedbackQuery.Where(feedback => feedback.CreatedAt >= from);
        }

        if (to is not null)
        {
            feedbackQuery = feedbackQuery.Where(feedback => feedback.CreatedAt <= to);
        }

        var feedbackCorrectCount = await feedbackQuery.CountAsync(feedback => feedback.Value == AiFeedbackValue.Correct, cancellationToken);
        var feedbackIncorrectCount = await feedbackQuery.CountAsync(feedback => feedback.Value == AiFeedbackValue.Incorrect, cancellationToken);
        var incorrectPendingCount = await feedbackQuery.CountAsync(
            feedback => feedback.Value == AiFeedbackValue.Incorrect && feedback.ReviewStatus != FeedbackReviewStatus.Resolved,
            cancellationToken);

        var sourceQuery = dbContext.AiInteractionSources.AsNoTracking();
        if (from is not null)
        {
            sourceQuery = sourceQuery.Where(source => source.CreatedAt >= from);
        }

        if (to is not null)
        {
            sourceQuery = sourceQuery.Where(source => source.CreatedAt <= to);
        }

        if (folderId is not null || teamId is not null)
        {
            var scopedDocumentIds = dbContext.Documents
                .AsNoTracking()
                .Where(document => document.DeletedAt == null && scopedFolderIds.Contains(document.FolderId))
                .Select(document => document.Id);

            sourceQuery = sourceQuery.Where(source => source.DocumentId != null && scopedDocumentIds.Contains(source.DocumentId.Value));
        }

        var topCitedSourceRows = await sourceQuery.GroupBy(source => new { source.SourceType, source.Title, source.FolderPath })
            .Select(group => new
            {
                group.Key.SourceType,
                group.Key.Title,
                group.Key.FolderPath,
                Count = group.Count(),
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Title)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCitedSources = topCitedSourceRows
            .Select(item => new TopCitedSourceResponse(
                item.SourceType.ToString(),
                item.Title,
                item.FolderPath,
                item.Count))
            .ToList();

        var evaluationCaseQuery = dbContext.EvaluationCases
            .AsNoTracking()
            .Where(evaluationCase => evaluationCase.IsActive);

        if (from is not null)
        {
            evaluationCaseQuery = evaluationCaseQuery.Where(evaluationCase => evaluationCase.CreatedAt >= from);
        }

        if (to is not null)
        {
            evaluationCaseQuery = evaluationCaseQuery.Where(evaluationCase => evaluationCase.CreatedAt <= to);
        }

        if (folderId is not null || teamId is not null)
        {
            var scopedDocumentIds = dbContext.Documents
                .AsNoTracking()
                .Where(document => document.DeletedAt == null && scopedFolderIds.Contains(document.FolderId))
                .Select(document => document.Id);

            evaluationCaseQuery = evaluationCaseQuery.Where(evaluationCase =>
                evaluationCase.ScopeType == AiScopeType.All ||
                (evaluationCase.ScopeType == AiScopeType.Folder && evaluationCase.FolderId != null && scopedFolderIds.Contains(evaluationCase.FolderId.Value)) ||
                (evaluationCase.ScopeType == AiScopeType.Document && evaluationCase.DocumentId != null && scopedDocumentIds.Contains(evaluationCase.DocumentId.Value)));
        }

        var evaluationCaseCount = await evaluationCaseQuery.CountAsync(cancellationToken);
        var latestEvaluationRunQuery = dbContext.EvaluationRuns.AsNoTracking();
        if (from is not null)
        {
            latestEvaluationRunQuery = latestEvaluationRunQuery.Where(run => run.CreatedAt >= from);
        }

        if (to is not null)
        {
            latestEvaluationRunQuery = latestEvaluationRunQuery.Where(run => run.CreatedAt <= to);
        }

        var latestEvaluationRun = await latestEvaluationRunQuery
            .OrderByDescending(run => run.FinishedAt ?? run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var latestEvaluationPassRate = latestEvaluationRun is null || latestEvaluationRun.TotalCases == 0
            ? (double?)null
            : Math.Round((double)latestEvaluationRun.PassedCases / latestEvaluationRun.TotalCases * 100, 2);

        return Ok(new DashboardSummaryResponse(
            documentCounts,
            wikiCounts,
            aiQuestionCount,
            feedbackCorrectCount,
            feedbackIncorrectCount,
            incorrectPendingCount,
            evaluationCaseCount,
            latestEvaluationRun?.TotalCases,
            latestEvaluationRun?.PassedCases,
            latestEvaluationPassRate,
            latestEvaluationRun?.FinishedAt ?? latestEvaluationRun?.CreatedAt,
            topCitedSources));
    }
}
