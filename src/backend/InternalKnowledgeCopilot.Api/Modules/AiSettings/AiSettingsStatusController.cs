using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.AiSettings;

[ApiController]
[Route("api/ai-settings")]
[Authorize]
public sealed class AiSettingsStatusController(IAiProviderSettingsService settingsService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<AiProviderConfigurationStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        return Ok(new AiProviderConfigurationStatusResponse(
            options.Name,
            !string.IsNullOrWhiteSpace(options.ApiKey),
            string.Equals(options.Name, "mock", StringComparison.OrdinalIgnoreCase)));
    }
}
