using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;

[ApiController]
[Route("api/knowledge-sources")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class KnowledgeSourcesController(IKnowledgeSourceService knowledgeSourceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSourceResponse>>> GetSources(
        [FromQuery] Guid? applicationId,
        CancellationToken cancellationToken)
    {
        return Ok(await knowledgeSourceService.GetSourcesAsync(applicationId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<KnowledgeSourceResponse>> UpsertSource(
        UpsertKnowledgeSourceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await knowledgeSourceService.UpsertSourceAsync(GetCurrentUserId(), request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Knowledge source request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "Knowledge source dependency was not found."));
        }
    }

    [HttpGet("external-objects")]
    public async Task<ActionResult<IReadOnlyList<ExternalObjectResponse>>> GetExternalObjects(
        [FromQuery] Guid? applicationId,
        [FromQuery] Guid? knowledgeSourceId,
        CancellationToken cancellationToken)
    {
        return Ok(await knowledgeSourceService.GetExternalObjectsAsync(applicationId, knowledgeSourceId, cancellationToken));
    }

    [HttpPost("external-objects")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ExternalObjectResponse>> UpsertExternalObject(
        UpsertExternalObjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await knowledgeSourceService.UpsertExternalObjectAsync(GetCurrentUserId(), request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "External object request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "External object dependency was not found."));
        }
    }

    [HttpPut("external-objects/acl-snapshots")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<ExternalAclSnapshotResponse>>> ReplaceAclSnapshots(
        ReplaceExternalAclSnapshotsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await knowledgeSourceService.ReplaceAclSnapshotsAsync(GetCurrentUserId(), request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "ACL snapshot request is invalid."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "External object was not found."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
