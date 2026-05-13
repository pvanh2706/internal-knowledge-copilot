using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Feedback;

public interface IAiFeedbackService
{
    Task<FeedbackResponse> SubmitAsync(Guid interactionId, Guid userId, SubmitFeedbackRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncorrectFeedbackResponse>> GetIncorrectAsync(CancellationToken cancellationToken = default);

    Task<FeedbackResponse> UpdateReviewStatusAsync(Guid feedbackId, Guid reviewerId, UpdateFeedbackReviewStatusRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QualityIssueResponse>> GetQualityIssuesAsync(CancellationToken cancellationToken = default);

    Task ClassifyIssueAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<KnowledgeCorrectionResponse> CreateCorrectionAsync(Guid issueId, Guid reviewerId, CreateCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeCorrectionResponse> ApproveCorrectionAsync(Guid correctionId, Guid reviewerId, CancellationToken cancellationToken = default);

    Task<KnowledgeCorrectionResponse> RejectCorrectionAsync(Guid correctionId, Guid reviewerId, RejectCorrectionRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiFeedbackService(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore) : IAiFeedbackService
{
    public async Task<FeedbackResponse> SubmitAsync(Guid interactionId, Guid userId, SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        var interactionExists = await dbContext.AiInteractions
            .AnyAsync(interaction => interaction.Id == interactionId && interaction.UserId == userId, cancellationToken);

        if (!interactionExists)
        {
            throw new KeyNotFoundException("interaction_not_found");
        }

        var now = DateTimeOffset.UtcNow;
        var feedback = await dbContext.AiFeedback
            .FirstOrDefaultAsync(item => item.AiInteractionId == interactionId && item.UserId == userId, cancellationToken);

        if (feedback is null)
        {
            feedback = new AiFeedbackEntity
            {
                Id = Guid.NewGuid(),
                AiInteractionId = interactionId,
                UserId = userId,
                CreatedAt = now,
            };
            dbContext.AiFeedback.Add(feedback);
        }

        feedback.Value = request.Value;
        feedback.Note = NormalizeNote(request.Note);
        feedback.ReviewStatus = request.Value == AiFeedbackValue.Incorrect
            ? FeedbackReviewStatus.New
            : FeedbackReviewStatus.Resolved;
        feedback.ReviewedByUserId = null;
        feedback.ReviewerNote = null;
        feedback.ResolvedAt = request.Value == AiFeedbackValue.Correct ? now : null;
        feedback.UpdatedAt = now;

        if (request.Value == AiFeedbackValue.Incorrect)
        {
            await EnsureQualityIssueAsync(feedback, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(userId, "AiFeedbackSubmitted", "AiInteraction", interactionId, new { request.Value }, cancellationToken);
        return ToResponse(feedback);
    }

    public async Task<IReadOnlyList<IncorrectFeedbackResponse>> GetIncorrectAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.AiFeedback
            .AsNoTracking()
            .Include(feedback => feedback.User)
            .Include(feedback => feedback.AiInteraction)
                .ThenInclude(interaction => interaction!.Sources)
            .Where(feedback => feedback.Value == AiFeedbackValue.Incorrect)
            .OrderBy(feedback => feedback.ReviewStatus == FeedbackReviewStatus.Resolved)
            .ThenBy(feedback => feedback.Id)
            .ToListAsync(cancellationToken);

        return items.Select(feedback => new IncorrectFeedbackResponse(
            feedback.Id,
            feedback.AiInteractionId,
            feedback.User?.DisplayName ?? "Unknown",
            feedback.AiInteraction?.Question ?? string.Empty,
            feedback.AiInteraction?.Answer ?? string.Empty,
            feedback.Note,
            feedback.ReviewStatus,
            feedback.ReviewerNote,
            feedback.CreatedAt,
            feedback.UpdatedAt,
            (feedback.AiInteraction?.Sources ?? [])
                .OrderBy(source => source.Rank)
                .Select(source => new FeedbackSourceResponse(
                    source.SourceType,
                    source.Title,
                    source.FolderPath,
                    source.SectionTitle,
                    source.Excerpt,
                    source.Rank))
                .ToList())).ToList();
    }

    public async Task<FeedbackResponse> UpdateReviewStatusAsync(Guid feedbackId, Guid reviewerId, UpdateFeedbackReviewStatusRequest request, CancellationToken cancellationToken = default)
    {
        var feedback = await dbContext.AiFeedback
            .FirstOrDefaultAsync(item => item.Id == feedbackId && item.Value == AiFeedbackValue.Incorrect, cancellationToken);

        if (feedback is null)
        {
            throw new KeyNotFoundException("feedback_not_found");
        }

        var now = DateTimeOffset.UtcNow;
        feedback.ReviewStatus = request.Status;
        feedback.ReviewedByUserId = reviewerId;
        feedback.ReviewerNote = NormalizeNote(request.ReviewerNote);
        feedback.ResolvedAt = request.Status == FeedbackReviewStatus.Resolved ? now : null;
        feedback.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "AiFeedbackReviewed", "AiFeedback", feedback.Id, new { request.Status }, cancellationToken);
        return ToResponse(feedback);
    }

    public async Task<IReadOnlyList<QualityIssueResponse>> GetQualityIssuesAsync(CancellationToken cancellationToken = default)
    {
        var issues = await dbContext.AiQualityIssues
            .AsNoTracking()
            .Include(issue => issue.AiFeedback)
            .Include(issue => issue.AiInteraction)
            .OrderBy(issue => issue.Status == AiQualityIssueStatus.Resolved)
            .ThenByDescending(issue => issue.CreatedAt)
            .ToListAsync(cancellationToken);
        var issueIds = issues.Select(issue => issue.Id).ToHashSet();
        var corrections = await dbContext.KnowledgeCorrections
            .AsNoTracking()
            .Where(correction => issueIds.Contains(correction.QualityIssueId))
            .OrderByDescending(correction => correction.CreatedAt)
            .ToListAsync(cancellationToken);
        var correctionsByIssue = corrections
            .GroupBy(correction => correction.QualityIssueId)
            .ToDictionary(group => group.Key, group => group.Select(ToCorrectionResponse).ToList());

        return issues.Select(issue => ToQualityIssueResponse(issue, correctionsByIssue)).ToList();
    }

    public async Task ClassifyIssueAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.AiQualityIssues
            .Include(item => item.AiFeedback)
            .Include(item => item.AiInteraction)
                .ThenInclude(interaction => interaction!.Sources)
            .FirstOrDefaultAsync(item => item.Id == issueId, cancellationToken);

        if (issue?.AiFeedback is null || issue.AiInteraction is null)
        {
            throw new KeyNotFoundException("quality_issue_not_found");
        }

        var classification = Classify(issue);
        var now = DateTimeOffset.UtcNow;
        issue.Status = AiQualityIssueStatus.Classified;
        issue.FailureType = classification.FailureType;
        issue.Severity = classification.Severity;
        issue.RootCauseHypothesis = classification.RootCauseHypothesis;
        issue.RecommendedActionsJson = JsonSerializer.Serialize(classification.RecommendedActions);
        issue.EvidenceJson = JsonSerializer.Serialize(classification.Evidence);
        issue.ClassifiedAt = now;
        issue.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KnowledgeCorrectionResponse> CreateCorrectionAsync(Guid issueId, Guid reviewerId, CreateCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.AiQualityIssues
            .Include(item => item.AiFeedback)
            .Include(item => item.AiInteraction)
                .ThenInclude(interaction => interaction!.Sources)
            .FirstOrDefaultAsync(item => item.Id == issueId, cancellationToken);

        if (issue?.AiFeedback is null || issue.AiInteraction is null)
        {
            throw new KeyNotFoundException("quality_issue_not_found");
        }

        var correctionText = NormalizeRequired(request.CorrectionText, "correction_text_required");
        if (request.VisibilityScope == VisibilityScope.Company && !request.IsCompanyPublicConfirmed)
        {
            throw new InvalidOperationException("company_public_confirmation_required");
        }

        var folderId = request.VisibilityScope == VisibilityScope.Folder
            ? request.FolderId ?? await InferFolderIdAsync(issue.AiInteraction, cancellationToken)
            : null;
        if (request.VisibilityScope == VisibilityScope.Folder && folderId is null)
        {
            throw new InvalidOperationException("folder_required");
        }

        var now = DateTimeOffset.UtcNow;
        var correction = new KnowledgeCorrectionEntity
        {
            Id = Guid.NewGuid(),
            QualityIssueId = issue.Id,
            AiFeedbackId = issue.AiFeedbackId,
            AiInteractionId = issue.AiInteractionId,
            Question = issue.AiInteraction.Question,
            CorrectionText = correctionText,
            VisibilityScope = request.VisibilityScope,
            FolderId = folderId,
            DocumentId = issue.AiInteraction.ScopeDocumentId ?? issue.AiInteraction.Sources.FirstOrDefault(source => source.DocumentId is not null)?.DocumentId,
            Status = KnowledgeCorrectionStatus.Draft,
            CreatedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        issue.Status = AiQualityIssueStatus.InReview;
        issue.UpdatedAt = now;
        dbContext.KnowledgeCorrections.Add(correction);
        dbContext.RetrievalHints.Add(new RetrievalHintEntity
        {
            Id = Guid.NewGuid(),
            CorrectionId = correction.Id,
            HintText = issue.AiInteraction.Question,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "KnowledgeCorrectionCreated", "KnowledgeCorrection", correction.Id, new { issue.Id }, cancellationToken);
        return ToCorrectionResponse(correction);
    }

    public async Task<KnowledgeCorrectionResponse> ApproveCorrectionAsync(Guid correctionId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var correction = await dbContext.KnowledgeCorrections
            .Include(item => item.QualityIssue)
            .Include(item => item.AiFeedback)
            .Include(item => item.Folder)
            .FirstOrDefaultAsync(item => item.Id == correctionId, cancellationToken);

        if (correction?.QualityIssue is null || correction.AiFeedback is null)
        {
            throw new KeyNotFoundException("correction_not_found");
        }

        if (correction.Status != KnowledgeCorrectionStatus.Draft)
        {
            throw new InvalidOperationException("correction_not_approvable");
        }

        var now = DateTimeOffset.UtcNow;
        correction.Status = KnowledgeCorrectionStatus.Approved;
        correction.ApprovedByUserId = reviewerId;
        correction.ApprovedAt = now;
        correction.IndexedAt = now;
        correction.UpdatedAt = now;
        correction.QualityIssue.Status = AiQualityIssueStatus.Resolved;
        correction.QualityIssue.ResolvedAt = now;
        correction.QualityIssue.UpdatedAt = now;
        correction.AiFeedback.ReviewStatus = FeedbackReviewStatus.Resolved;
        correction.AiFeedback.ReviewedByUserId = reviewerId;
        correction.AiFeedback.ResolvedAt = now;
        correction.AiFeedback.UpdatedAt = now;
        correction.AiFeedback.ReviewerNote ??= "Approved correction.";

        await IndexCorrectionAsync(correction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "KnowledgeCorrectionApproved", "KnowledgeCorrection", correction.Id, new { correction.QualityIssueId }, cancellationToken);
        return ToCorrectionResponse(correction);
    }

    public async Task<KnowledgeCorrectionResponse> RejectCorrectionAsync(Guid correctionId, Guid reviewerId, RejectCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var correction = await dbContext.KnowledgeCorrections
            .FirstOrDefaultAsync(item => item.Id == correctionId, cancellationToken);

        if (correction is null)
        {
            throw new KeyNotFoundException("correction_not_found");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("reject_reason_required");
        }

        correction.Status = KnowledgeCorrectionStatus.Rejected;
        correction.RejectReason = request.Reason.Trim();
        correction.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "KnowledgeCorrectionRejected", "KnowledgeCorrection", correction.Id, new { correction.RejectReason }, cancellationToken);
        return ToCorrectionResponse(correction);
    }

    private async Task EnsureQualityIssueAsync(AiFeedbackEntity feedback, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existingIssue = await dbContext.AiQualityIssues
            .AnyAsync(issue => issue.AiFeedbackId == feedback.Id, cancellationToken);
        if (existingIssue)
        {
            return;
        }

        var issue = new AiQualityIssueEntity
        {
            Id = Guid.NewGuid(),
            AiFeedbackId = feedback.Id,
            AiInteractionId = feedback.AiInteractionId,
            Status = AiQualityIssueStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.AiQualityIssues.Add(issue);
        dbContext.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            JobType = "ClassifyAiFailure",
            TargetType = "AiQualityIssue",
            TargetId = issue.Id,
            Status = ProcessingJobStatus.Pending,
            Attempts = 0,
            CreatedAt = now,
        });
    }

    private FailureClassification Classify(AiQualityIssueEntity issue)
    {
        var note = issue.AiFeedback?.Note ?? string.Empty;
        var interaction = issue.AiInteraction!;
        var sources = interaction.Sources;
        var lowerNote = note.ToLowerInvariant();
        var failureType = sources.Count == 0
            ? "NoRelevantContext"
            : interaction.NeedsClarification
                ? "AmbiguousQuestion"
                : "BadAnswer";

        if (lowerNote.Contains("permission", StringComparison.OrdinalIgnoreCase) || lowerNote.Contains("quyen", StringComparison.OrdinalIgnoreCase))
        {
            failureType = "PermissionIssue";
        }
        else if (lowerNote.Contains("outdated", StringComparison.OrdinalIgnoreCase) || lowerNote.Contains("cu", StringComparison.OrdinalIgnoreCase))
        {
            failureType = "OutdatedContext";
        }
        else if (lowerNote.Contains("missing", StringComparison.OrdinalIgnoreCase) || lowerNote.Contains("thieu", StringComparison.OrdinalIgnoreCase))
        {
            failureType = sources.Count == 0 ? "MissingDocument" : "MissingInformation";
        }

        var severity = failureType is "PermissionIssue" or "BadAnswer" ? "high" : "medium";
        var actions = failureType switch
        {
            "NoRelevantContext" or "MissingDocument" => new[] { "UploadMissingDocument", "CreateCorrection", "AskClarification" },
            "OutdatedContext" => ["CreateCorrection", "ReindexDocument", "RegenerateWiki"],
            "PermissionIssue" => ["ReviewPermissions", "CreateCorrection"],
            _ => ["CreateCorrection", "RegenerateWiki", "AskClarification"],
        };
        var hypothesis = failureType switch
        {
            "NoRelevantContext" => "No approved source was cited for the answer.",
            "MissingDocument" => "The knowledge base likely misses the document or section needed to answer.",
            "OutdatedContext" => "The answer may have used outdated or incomplete context.",
            "PermissionIssue" => "The answer may be affected by an access-control or scope mismatch.",
            "AmbiguousQuestion" => "The question likely needs more context before retrieval can be reliable.",
            _ => "The retrieved context was present, but the generated answer may be incomplete or wrong.",
        };

        var evidence = new
        {
            interaction.Question,
            interaction.Answer,
            UserNote = note,
            UsedSources = sources.Select(source => new { source.SourceType, source.Title, source.FolderPath, source.SectionTitle }).ToArray(),
        };

        return new FailureClassification(failureType, severity, hypothesis, actions, evidence);
    }

    private async Task<Guid?> InferFolderIdAsync(AiInteractionEntity interaction, CancellationToken cancellationToken)
    {
        if (interaction.ScopeFolderId is not null)
        {
            return interaction.ScopeFolderId;
        }

        var documentId = interaction.ScopeDocumentId ?? interaction.Sources.FirstOrDefault(source => source.DocumentId is not null)?.DocumentId;
        if (documentId is null)
        {
            return null;
        }

        return await dbContext.Documents
            .AsNoTracking()
            .Where(document => document.Id == documentId && document.DeletedAt == null)
            .Select(document => (Guid?)document.FolderId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task IndexCorrectionAsync(KnowledgeCorrectionEntity correction, CancellationToken cancellationToken)
    {
        var text = $"""
            Question:
            {correction.Question}

            Approved correction:
            {correction.CorrectionText}
            """;
        var embedding = await embeddingService.CreateEmbeddingAsync(text, cancellationToken);
        var folderPath = correction.Folder?.Path ?? string.Empty;
        await vectorStore.UpsertChunksAsync(
            [
                new KnowledgeChunkRecord(
                    correction.Id.ToString(),
                    embedding,
                    text,
                    new Dictionary<string, object>
                    {
                        ["chunk_id"] = correction.Id.ToString(),
                        ["source_type"] = "correction",
                        ["source_id"] = correction.Id.ToString(),
                        ["correction_id"] = correction.Id.ToString(),
                        ["feedback_id"] = correction.AiFeedbackId.ToString(),
                        ["ai_interaction_id"] = correction.AiInteractionId.ToString(),
                        ["document_id"] = correction.DocumentId?.ToString() ?? string.Empty,
                        ["folder_id"] = correction.FolderId?.ToString() ?? string.Empty,
                        ["title"] = $"Correction: {Trim(correction.Question, 120)}",
                        ["folder_path"] = folderPath,
                        ["status"] = "approved",
                        ["visibility_scope"] = correction.VisibilityScope == VisibilityScope.Company ? "company" : "folder",
                        ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                    }),
            ],
            cancellationToken);
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeRequired(string value, string errorCode)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException(errorCode);
        }

        return trimmed;
    }

    private static string Trim(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";
    }

    private static IReadOnlyList<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static FeedbackResponse ToResponse(AiFeedbackEntity feedback)
    {
        return new FeedbackResponse(
            feedback.Id,
            feedback.AiInteractionId,
            feedback.Value,
            feedback.Note,
            feedback.ReviewStatus,
            feedback.ReviewerNote,
            feedback.CreatedAt,
            feedback.UpdatedAt);
    }

    private static QualityIssueResponse ToQualityIssueResponse(
        AiQualityIssueEntity issue,
        IReadOnlyDictionary<Guid, List<KnowledgeCorrectionResponse>> correctionsByIssue)
    {
        return new QualityIssueResponse(
            issue.Id,
            issue.AiFeedbackId,
            issue.AiInteractionId,
            issue.AiInteraction?.Question ?? string.Empty,
            issue.AiInteraction?.Answer ?? string.Empty,
            issue.AiFeedback?.Note,
            issue.Status,
            issue.FailureType,
            issue.Severity,
            issue.RootCauseHypothesis,
            ParseStringList(issue.RecommendedActionsJson),
            issue.CreatedAt,
            issue.UpdatedAt,
            correctionsByIssue.TryGetValue(issue.Id, out var corrections) ? corrections : []);
    }

    private static KnowledgeCorrectionResponse ToCorrectionResponse(KnowledgeCorrectionEntity correction)
    {
        return new KnowledgeCorrectionResponse(
            correction.Id,
            correction.QualityIssueId,
            correction.Question,
            correction.CorrectionText,
            correction.VisibilityScope,
            correction.FolderId,
            correction.DocumentId,
            correction.Status,
            correction.RejectReason,
            correction.CreatedAt,
            correction.UpdatedAt,
            correction.ApprovedAt);
    }

    private sealed record FailureClassification(
        string FailureType,
        string Severity,
        string RootCauseHypothesis,
        IReadOnlyList<string> RecommendedActions,
        object Evidence);
}
