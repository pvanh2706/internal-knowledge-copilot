using InternalKnowledgeCopilot.Api.Modules.Auth;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void VerifyPassword_ReturnsTrue_ForMatchingPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("ChangeMe123!");

        Assert.True(hasher.VerifyPassword(hash, "ChangeMe123!"));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("ChangeMe123!");

        Assert.False(hasher.VerifyPassword(hash, "wrong-password"));
    }
}
