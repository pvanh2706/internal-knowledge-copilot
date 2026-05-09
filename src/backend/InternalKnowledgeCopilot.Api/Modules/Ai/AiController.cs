using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Ai;

[ApiController]
[Route("api/ai")]
[Authorize]
public sealed class AiController(IAiQuestionService aiQuestionService, IAiFeedbackService feedbackService) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<AskQuestionResponse>> Ask(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await aiQuestionService.AskAsync(userId.Value, request, cancellationToken));
        }
        catch (ArgumentException ex) when (ex.Message == "question_required")
        {
            return BadRequest(new ApiError("question_required", "Câu hỏi là bắt buộc."));
        }
        catch (ArgumentException ex) when (ex.Message == "folder_required")
        {
            return BadRequest(new ApiError("folder_required", "Vui lòng chọn folder cho phạm vi Folder."));
        }
        catch (ArgumentException ex) when (ex.Message == "document_required")
        {
            return BadRequest(new ApiError("document_required", "Vui lòng chọn tài liệu cho phạm vi Document."));
        }
        catch (KeyNotFoundException ex) when (ex.Message == "document_not_found")
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("interactions/{id:guid}/feedback")]
    public async Task<ActionResult<FeedbackResponse>> SubmitFeedback(Guid id, SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await feedbackService.SubmitAsync(id, userId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("interaction_not_found", "Không tìm thấy lượt hỏi AI."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
