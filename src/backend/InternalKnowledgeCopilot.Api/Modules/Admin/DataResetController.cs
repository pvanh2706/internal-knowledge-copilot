using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Admin;

[ApiController]
[Route("api/admin/data-reset")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class DataResetController(IDataResetService dataResetService) : ControllerBase
{
    [HttpGet]
    public ActionResult<DataResetStatusResponse> GetStatus()
    {
        return Ok(new DataResetStatusResponse(
            dataResetService.IsEnabled,
            dataResetService.ConfirmationPhrase,
            KeepsUsersTeamsAndAiSettings: true));
    }

    [HttpPost]
    public async Task<ActionResult<DataResetResponse>> Reset(DataResetRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (adminUserId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token khong hop le."));
        }

        try
        {
            return Ok(await dataResetService.ResetAsync(adminUserId.Value, request, cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message == "data_reset_disabled")
        {
            return BadRequest(new ApiError("data_reset_disabled", "Chuc nang reset du lieu dang bi tat trong cau hinh."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "invalid_confirmation_phrase")
        {
            return BadRequest(new ApiError("invalid_confirmation_phrase", "Chuoi xac nhan reset khong dung."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "unsafe_storage_root")
        {
            return BadRequest(new ApiError("unsafe_storage_root", "Storage root khong an toan de xoa du lieu."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
