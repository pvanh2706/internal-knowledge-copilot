using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Feedback;

[ApiController]
[Route("api/feedback")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class FeedbackController(IAiFeedbackService feedbackService) : ControllerBase
{
    [HttpGet("incorrect")]
    public async Task<ActionResult<IReadOnlyList<IncorrectFeedbackResponse>>> GetIncorrect(CancellationToken cancellationToken)
    {
        return Ok(await feedbackService.GetIncorrectAsync(cancellationToken));
    }

    [HttpPatch("{id:guid}/review-status")]
    public async Task<ActionResult<FeedbackResponse>> UpdateReviewStatus(Guid id, UpdateFeedbackReviewStatusRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await feedbackService.UpdateReviewStatusAsync(id, reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("feedback_not_found", "Không tìm thấy feedback sai."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
