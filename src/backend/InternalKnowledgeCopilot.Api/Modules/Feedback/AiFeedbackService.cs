using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Feedback;

public interface IAiFeedbackService
{
    Task<FeedbackResponse> SubmitAsync(Guid interactionId, Guid userId, SubmitFeedbackRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncorrectFeedbackResponse>> GetIncorrectAsync(CancellationToken cancellationToken = default);

    Task<FeedbackResponse> UpdateReviewStatusAsync(Guid feedbackId, Guid reviewerId, UpdateFeedbackReviewStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiFeedbackService(AppDbContext dbContext, IAuditLogService auditLogService) : IAiFeedbackService
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

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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
}
