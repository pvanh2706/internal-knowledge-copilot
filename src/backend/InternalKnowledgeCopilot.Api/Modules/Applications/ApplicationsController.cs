using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Applications;

[ApiController]
[Route("api/admin/applications")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class ApplicationsController(IApplicationService applicationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> GetApplications(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        return Ok(await applicationService.GetApplicationsAsync(tenantId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> GetApplication(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await applicationService.GetApplicationAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Application was not found."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> CreateApplication(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await applicationService.CreateApplicationAsync(GetCurrentUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetApplication), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Application request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Tenant was not found."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(ex.Message, "Application could not be created."));
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> UpdateApplication(
        Guid id,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await applicationService.UpdateApplicationAsync(id, GetCurrentUserId(), request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Application request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Application was not found."));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await applicationService.DeleteApplicationAsync(id, GetCurrentUserId(), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Application was not found."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
