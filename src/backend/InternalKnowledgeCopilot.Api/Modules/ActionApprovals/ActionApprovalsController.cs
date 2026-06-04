using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.ActionApprovals;

[ApiController]
[Route("api/action-approvals")]
[Authorize]
public sealed class ActionApprovalsController(IActionApprovalService actionApprovalService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiActionRequestResponse>>> GetActions(
        [FromQuery] Guid? applicationId,
        [FromQuery] AiActionRequestStatus? status,
        [FromQuery] Guid? recommendationId,
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
            return Ok(await actionApprovalService.GetActionsAsync(
                userId.Value,
                applicationId,
                status,
                recommendationId,
                objectType,
                externalObjectId,
                cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Action query is invalid."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("recommendations/{recommendationId:guid}/actions")]
    public Task<ActionResult<AiActionRequestResponse>> CreateAction(
        Guid recommendationId,
        [FromBody] CreateAiActionRequest request,
        CancellationToken cancellationToken)
    {
        return HandleActionAsync(userId => actionApprovalService.CreateActionAsync(
            recommendationId,
            userId,
            request,
            cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    public Task<ActionResult<AiActionRequestResponse>> ApproveAction(
        Guid id,
        [FromBody] ApproveAiActionRequest request,
        CancellationToken cancellationToken)
    {
        return HandleActionAsync(userId => actionApprovalService.ApproveActionAsync(id, userId, request, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<AiActionRequestResponse>> RejectAction(
        Guid id,
        [FromBody] RejectAiActionRequest request,
        CancellationToken cancellationToken)
    {
        return HandleActionAsync(userId => actionApprovalService.RejectActionAsync(id, userId, request, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<AiActionRequestResponse>> CancelAction(
        Guid id,
        [FromBody] CancelAiActionRequest request,
        CancellationToken cancellationToken)
    {
        return HandleActionAsync(userId => actionApprovalService.CancelActionAsync(id, userId, request, cancellationToken));
    }

    [HttpPost("{id:guid}/execute")]
    public Task<ActionResult<AiActionRequestResponse>> ExecuteAction(Guid id, CancellationToken cancellationToken)
    {
        return HandleActionAsync(userId => actionApprovalService.ExecuteActionAsync(id, userId, cancellationToken));
    }

    private async Task<ActionResult<AiActionRequestResponse>> HandleActionAsync(Func<Guid, Task<AiActionRequestResponse>> handler)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token is invalid."));
        }

        try
        {
            return Ok(await handler(userId.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Action request is invalid."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Action request cannot be processed."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Action dependency was not found."));
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
