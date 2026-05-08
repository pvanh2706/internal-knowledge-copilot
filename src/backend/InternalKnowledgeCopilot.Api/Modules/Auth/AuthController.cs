using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext dbContext, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email && item.DeletedAt == null, cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            return Unauthorized(new ApiError("invalid_credentials", "Email hoặc mật khẩu không đúng."));
        }

        var accessToken = jwtTokenService.CreateAccessToken(user);
        return Ok(new LoginResponse(
            accessToken,
            user.MustChangePassword,
            new AuthUserResponse(user.Id, user.DisplayName, user.Role)));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId && item.DeletedAt == null, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new ApiError("invalid_current_password", "Mật khẩu hiện tại không đúng."));
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
