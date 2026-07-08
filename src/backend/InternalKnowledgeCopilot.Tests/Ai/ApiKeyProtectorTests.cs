using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class ApiKeyProtectorTests
{
    private static DataProtectionApiKeyProtector CreateProtector()
    {
        return new DataProtectionApiKeyProtector(new EphemeralDataProtectionProvider());
    }

    [Fact]
    public void Protect_RoiUnprotect_TraVeGiaTriGoc()
    {
        var protector = CreateProtector();
        const string plaintext = "sk-test-api-key-12345";

        var stored = protector.Protect(plaintext);
        var restored = protector.Unprotect(stored);

        Assert.NotNull(stored);
        Assert.StartsWith(DataProtectionApiKeyProtector.ProtectedPrefix, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(plaintext, stored, StringComparison.Ordinal);
        Assert.Equal(plaintext, restored);
    }

    [Fact]
    public void Unprotect_GiaTriPlaintextCu_TraVeNguyenTrang()
    {
        var protector = CreateProtector();

        Assert.Equal("legacy-plaintext-key", protector.Unprotect("legacy-plaintext-key"));
    }

    [Fact]
    public void Protect_GiaTriRongHoacNull_TraVeNguyenTrang()
    {
        var protector = CreateProtector();

        Assert.Null(protector.Protect(null));
        Assert.Equal(string.Empty, protector.Protect(string.Empty));
    }

    [Fact]
    public void Protect_GiaTriDaMaHoa_KhongMaHoaLong()
    {
        var protector = CreateProtector();

        var storedOnce = protector.Protect("sk-test-api-key-12345");
        var storedTwice = protector.Protect(storedOnce);

        Assert.Equal(storedOnce, storedTwice);
    }

    [Fact]
    public void IsProtected_PhanBietDungPlaintextVaDaMaHoa()
    {
        var protector = CreateProtector();

        Assert.True(protector.IsProtected(protector.Protect("sk-test")));
        Assert.False(protector.IsProtected("sk-test"));
        Assert.False(protector.IsProtected(null));
    }
}
