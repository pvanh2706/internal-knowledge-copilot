using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

public sealed record PlatformAccessToken(string Token, DateTimeOffset ExpiresAt, string SessionType);

public interface IPlatformJwtTokenService
{
    PlatformAccessToken CreateAccessToken(PlatformAdminEntity admin);
}

// Token platform không chứa tenant_id/tenant_code/role; sub là platform_admins.id. Không có refresh token.
public sealed class PlatformJwtTokenService(IOptions<PlatformJwtOptions> options) : IPlatformJwtTokenService
{
    public PlatformAccessToken CreateAccessToken(PlatformAdminEntity admin)
    {
        var jwtOptions = options.Value;
        var sessionType = admin.MustChangePassword ? PlatformSessionTypes.PasswordChangeOnly : PlatformSessionTypes.Full;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(Math.Clamp(jwtOptions.AccessTokenMinutes, 1, PlatformJwtOptions.MaxAccessTokenMinutes));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(PrincipalClaimTypes.PrincipalType, PrincipalTypes.Platform),
            new Claim(PrincipalClaimTypes.TokenVersion, admin.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            new Claim(PrincipalClaimTypes.PlatformSession, sessionType),
        };
        var token = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, now, expiresAt, credentials);
        return new PlatformAccessToken(new JwtSecurityTokenHandler().WriteToken(token), new DateTimeOffset(expiresAt, TimeSpan.Zero), sessionType);
    }
}
