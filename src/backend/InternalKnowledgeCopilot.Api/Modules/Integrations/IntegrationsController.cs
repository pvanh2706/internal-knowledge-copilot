using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Integrations;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController(IIntegrationService integrationService) : ControllerBase
{
    private const string IntegrationKeyHeaderName = "X-Integration-Key";
    private const string IntegrationKeyIdHeaderName = "X-Integration-Key-Id";

    [HttpGet("connections")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<ActionResult<IReadOnlyList<IntegrationConnectionResponse>>> GetConnections(
        [FromQuery] Guid? applicationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await integrationService.GetConnectionsAsync(applicationId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Tenant context is required."));
        }
    }

    [HttpPost("connections")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IntegrationConnectionResponse>> CreateConnection(
        [FromBody] CreateIntegrationConnectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await integrationService.CreateConnectionAsync(GetCurrentUserId(), request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Integration connection request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Integration connection dependency was not found."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(ex.Message, "Integration connection could not be created."));
        }
    }

    [HttpPost("{applicationCode}/events")]
    public Task<ActionResult<IntegrationInboundEventResponse>> ReceiveDomainEvent(
        string applicationCode,
        [FromBody] DomainIntegrationEventRequest request,
        CancellationToken cancellationToken)
    {
        return HandleInboundAsync(() => integrationService.ReceiveDomainEventAsync(
            applicationCode,
            GetIntegrationAuthentication(),
            request,
            cancellationToken));
    }

    [HttpPost("{applicationCode}/documents/changed")]
    public Task<ActionResult<IntegrationInboundEventResponse>> ReceiveDocumentChanged(
        string applicationCode,
        [FromBody] DocumentChangedIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        return HandleInboundAsync(() => integrationService.ReceiveDocumentChangedAsync(
            applicationCode,
            GetIntegrationAuthentication(),
            request,
            cancellationToken));
    }

    [HttpPost("{applicationCode}/objects/sync")]
    public Task<ActionResult<IntegrationInboundEventResponse>> ReceiveObjectSync(
        string applicationCode,
        [FromBody] ObjectSyncIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        return HandleInboundAsync(() => integrationService.ReceiveObjectSyncAsync(
            applicationCode,
            GetIntegrationAuthentication(),
            request,
            cancellationToken));
    }

    [HttpPost("{applicationCode}/permissions/sync")]
    public Task<ActionResult<IntegrationInboundEventResponse>> ReceivePermissionSync(
        string applicationCode,
        [FromBody] PermissionSyncIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        return HandleInboundAsync(() => integrationService.ReceivePermissionSyncAsync(
            applicationCode,
            GetIntegrationAuthentication(),
            request,
            cancellationToken));
    }

    private async Task<ActionResult<IntegrationInboundEventResponse>> HandleInboundAsync(Func<Task<IntegrationInboundEventResponse>> handler)
    {
        try
        {
            return Ok(await handler());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Integration request is invalid."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Tenant context is required."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Integration target was not found."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError(ex.Message, "Integration authentication failed."));
        }
    }

    private IntegrationAuthenticationRequest GetIntegrationAuthentication()
    {
        var keyId = Request.Headers[IntegrationKeyIdHeaderName].FirstOrDefault();
        var apiKey = Request.Headers[IntegrationKeyHeaderName].FirstOrDefault();
        return new IntegrationAuthenticationRequest(keyId, apiKey);
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
