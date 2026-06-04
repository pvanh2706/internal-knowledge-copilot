using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Auth;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ReturnsJwt()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            SigningKey = "local-development-signing-key-change-before-production",
        }));

        var tenantId = Guid.NewGuid();
        var token = service.CreateAccessToken(new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "admin@example.local",
            DisplayName = "Admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
        }, tenantId, "default");

        Assert.Contains(".", token);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(tenantId.ToString(), jwt.Claims.Single(claim => claim.Type == TenantClaimTypes.TenantId).Value);
        Assert.Equal("default", jwt.Claims.Single(claim => claim.Type == TenantClaimTypes.TenantCode).Value);
    }
}
