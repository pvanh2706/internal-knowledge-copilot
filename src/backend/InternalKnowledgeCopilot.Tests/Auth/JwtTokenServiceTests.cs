using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using Microsoft.Extensions.Options;
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

        var token = service.CreateAccessToken(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.local",
            DisplayName = "Admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
        });

        Assert.Contains(".", token);
    }
}
