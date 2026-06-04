using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Tenants;

[ApiController]
[Route("api/admin/tenants")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class TenantsController(ITenantService tenantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantResponse>>> GetTenants(CancellationToken cancellationToken)
    {
        return Ok(await tenantService.GetTenantsAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantResponse>> GetTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tenantService.GetTenantAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Tenant was not found."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<TenantResponse>> CreateTenant(
        CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantService.CreateTenantAsync(GetCurrentUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetTenant), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Tenant request is invalid."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(ex.Message, "Tenant could not be created."));
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TenantResponse>> UpdateTenant(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tenantService.UpdateTenantAsync(id, GetCurrentUserId(), request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Tenant request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Tenant was not found."));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await tenantService.DeleteTenantAsync(id, GetCurrentUserId(), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Tenant was not found."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(ex.Message, "Tenant could not be deleted."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
