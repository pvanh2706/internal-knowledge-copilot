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

    [HttpGet("quality-issues")]
    public async Task<ActionResult<IReadOnlyList<QualityIssueResponse>>> GetQualityIssues(CancellationToken cancellationToken)
    {
        return Ok(await feedbackService.GetQualityIssuesAsync(cancellationToken));
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

    [HttpPost("quality-issues/{id:guid}/corrections")]
    public async Task<ActionResult<KnowledgeCorrectionResponse>> CreateCorrection(Guid id, CreateCorrectionRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khÃ´ng há»£p lá»‡."));
        }

        try
        {
            return Ok(await feedbackService.CreateCorrectionAsync(id, reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("quality_issue_not_found", "KhÃ´ng tÃ¬m tháº¥y quality issue."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Dá»¯ liá»‡u correction khÃ´ng há»£p lá»‡."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "KhÃ´ng thá»ƒ táº¡o correction."));
        }
    }

    [HttpPost("corrections/{id:guid}/approve")]
    public async Task<ActionResult<KnowledgeCorrectionResponse>> ApproveCorrection(Guid id, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khÃ´ng há»£p lá»‡."));
        }

        try
        {
            return Ok(await feedbackService.ApproveCorrectionAsync(id, reviewerId.Value, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("correction_not_found", "KhÃ´ng tÃ¬m tháº¥y correction."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "KhÃ´ng thá»ƒ approve correction."));
        }
    }

    [HttpPost("corrections/{id:guid}/reject")]
    public async Task<ActionResult<KnowledgeCorrectionResponse>> RejectCorrection(Guid id, RejectCorrectionRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khÃ´ng há»£p lá»‡."));
        }

        try
        {
            return Ok(await feedbackService.RejectCorrectionAsync(id, reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("correction_not_found", "KhÃ´ng tÃ¬m tháº¥y correction."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Dá»¯ liá»‡u reject khÃ´ng há»£p lá»‡."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
