using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Evaluation;

[ApiController]
[Route("api/evaluation")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class EvaluationController(IEvaluationService evaluationService) : ControllerBase
{
    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<EvaluationCaseResponse>>> GetCases(CancellationToken cancellationToken)
    {
        return Ok(await evaluationService.GetCasesAsync(cancellationToken));
    }

    [HttpPost("feedback/{feedbackId:guid}/cases")]
    public async Task<ActionResult<EvaluationCaseResponse>> CreateCaseFromFeedback(
        Guid feedbackId,
        CreateEvaluationCaseFromFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khong hop le."));
        }

        try
        {
            return Ok(await evaluationService.CreateCaseFromFeedbackAsync(feedbackId, reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("feedback_not_found", "Khong tim thay feedback."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Du lieu eval case khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Khong the tao eval case tu feedback nay."));
        }
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<EvaluationRunResponse>>> GetRuns(CancellationToken cancellationToken)
    {
        return Ok(await evaluationService.GetRunsAsync(cancellationToken));
    }

    [HttpPost("runs")]
    public async Task<ActionResult<EvaluationRunResponse>> Run(RunEvaluationRequest? request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khong hop le."));
        }

        try
        {
            return Ok(await evaluationService.RunAsync(reviewerId.Value, request ?? new RunEvaluationRequest(null, null), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("evaluation_case_not_found", "Khong tim thay eval case."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Chua co eval case active de chay."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
