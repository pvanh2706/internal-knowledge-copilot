using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Operations;

[ApiController]
[Route("api/operations/metrics")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class OperationalMetricsController(IOperationalMetricsService metricsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperationalMetricsResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await metricsService.GetAsync(cancellationToken));
    }
}
