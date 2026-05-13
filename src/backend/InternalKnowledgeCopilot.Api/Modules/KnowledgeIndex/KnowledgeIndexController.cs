using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;

[ApiController]
[Route("api/knowledge-index")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class KnowledgeIndexController(IKnowledgeIndexRebuildService rebuildService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<KnowledgeIndexSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await rebuildService.GetSummaryAsync(cancellationToken));
    }

    [HttpPost("rebuild")]
    public async Task<ActionResult<RebuildKnowledgeIndexResponse>> Rebuild(
        RebuildKnowledgeIndexRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khÃ´ng há»£p lá»‡."));
        }

        return Ok(await rebuildService.RebuildAsync(userId.Value, request, cancellationToken));
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
