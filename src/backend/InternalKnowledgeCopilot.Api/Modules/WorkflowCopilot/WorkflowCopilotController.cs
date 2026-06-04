using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.WorkflowCopilot;

[ApiController]
[Route("api/workflow-copilot")]
[Authorize]
public sealed class WorkflowCopilotController(IWorkflowCopilotService workflowCopilotService) : ControllerBase
{
    [HttpPost("deal-stage-changed")]
    public async Task<ActionResult<WorkflowRecommendationResponse>> ReceiveDealStageChanged(
        [FromBody] DealStageChangedWorkflowEventRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token is invalid."));
        }

        try
        {
            return Ok(await workflowCopilotService.HandleDealStageChangedAsync(userId.Value, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Workflow event request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Workflow target was not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<IReadOnlyList<WorkflowRecommendationResponse>>> GetRecommendations(
        [FromQuery] Guid? applicationId,
        [FromQuery] string? objectType,
        [FromQuery] string? externalObjectId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token is invalid."));
        }

        try
        {
            return Ok(await workflowCopilotService.GetRecommendationsAsync(userId.Value, applicationId, objectType, externalObjectId, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Recommendation query is invalid."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("recommendations/{id:guid}/feedback")]
    public async Task<ActionResult<WorkflowRecommendationResponse>> SubmitRecommendationFeedback(
        Guid id,
        [FromBody] WorkflowRecommendationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token is invalid."));
        }

        try
        {
            return Ok(await workflowCopilotService.RecordRecommendationFeedbackAsync(id, userId.Value, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Recommendation feedback is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Recommendation was not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
