using System.Security.Cryptography;
using System.Text;

namespace InternalKnowledgeCopilot.Api.Modules.Integrations;

public interface IIntegrationSecretHasher
{
    string HashSecret(string secret);

    bool VerifySecret(string secret, string? expectedHash);
}

public sealed class IntegrationSecretHasher : IIntegrationSecretHasher
{
    public string HashSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("integration_secret_required");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()));
        return Convert.ToBase64String(bytes);
    }

    public bool VerifySecret(string secret, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualBytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()));
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
