using System.Globalization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

// Không có endpoint đăng ký/bootstrap công khai: tài khoản Platform Admin chỉ được tạo qua S01-C.
[ApiController]
[Route("api/platform/auth")]
public sealed class PlatformAuthController(IPlatformAuthService platformAuthService) : ControllerBase
{
    public const string RateLimitPolicy = "platform-auth";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<PlatformLoginResponse>> Login(PlatformLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await platformAuthService.LoginAsync(request.Email, request.Password, HttpContext.TraceIdentifier, cancellationToken);
        return result.Status switch
        {
            PlatformLoginStatus.Succeeded => Ok(new PlatformLoginResponse(
                result.Token!.Token,
                result.Token.ExpiresAt,
                result.MustChangePassword,
                result.Token.SessionType)),
            PlatformLoginStatus.Unavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiError("platform_auth_unavailable", "Đăng nhập quản trị platform chưa sẵn sàng.")),
            _ => Unauthorized(new ApiError("invalid_credentials", "Email hoặc mật khẩu không đúng.")),
        };
    }

    [HttpGet("me")]
    [Authorize(Policy = AppAuthorizationPolicies.PlatformPasswordChange)]
    public async Task<ActionResult<PlatformMeResponse>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetPrincipal(out var adminId, out _, out var sessionType))
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var profile = await platformAuthService.GetProfileAsync(adminId, sessionType, cancellationToken);
        return profile is null ? Unauthorized(new ApiError("invalid_token", "Token không hợp lệ.")) : Ok(profile);
    }

    // Sau khi đổi thành công, token_version tăng nên mọi token cũ (kể cả token hiện tại) hết hiệu lực; client đăng nhập lại.
    [HttpPost("change-password")]
    [Authorize(Policy = AppAuthorizationPolicies.PlatformPasswordChange)]
    public async Task<IActionResult> ChangePassword(PlatformChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPrincipal(out var adminId, out var tokenVersion, out _))
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var status = await platformAuthService.ChangePasswordAsync(
            adminId,
            tokenVersion,
            request.CurrentPassword,
            request.NewPassword,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return status switch
        {
            PlatformPasswordChangeStatus.Changed => NoContent(),
            PlatformPasswordChangeStatus.InvalidCurrentPassword => BadRequest(new ApiError("invalid_current_password", "Mật khẩu hiện tại không đúng.")),
            PlatformPasswordChangeStatus.PolicyViolation => BadRequest(new ApiError(
                "password_policy_violation",
                $"Mật khẩu mới phải dài {PlatformAuthService.MinPasswordLength}-{PlatformAuthService.MaxPasswordLength} ký tự và khác mật khẩu hiện tại.")),
            _ => Unauthorized(new ApiError("invalid_token", "Token không hợp lệ.")),
        };
    }

    private bool TryGetPrincipal(out Guid adminId, out int tokenVersion, out string sessionType)
    {
        sessionType = User.FindFirst(PrincipalClaimTypes.PlatformSession)?.Value ?? string.Empty;
        tokenVersion = 0;
        return Guid.TryParse(User.FindFirst("sub")?.Value, out adminId)
            && int.TryParse(User.FindFirst(PrincipalClaimTypes.TokenVersion)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out tokenVersion);
    }
}
