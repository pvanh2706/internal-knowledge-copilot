using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.AiSettings;

[ApiController]
[Route("api/admin/ai-settings")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AiSettingsController(IAiProviderSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AiProviderSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await settingsService.GetForAdminAsync(cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<AiProviderSettingsResponse>> Update(
        UpdateAiProviderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khong hop le."));
        }

        try
        {
            return Ok(await settingsService.UpdateAsync(userId.Value, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message, "Cau hinh AI provider khong hop le."));
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult<TestAiProviderSettingsResponse>> Test(CancellationToken cancellationToken)
    {
        return Ok(await settingsService.TestAsync(cancellationToken));
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
