namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

// Chặn khởi động khi cấu hình JWT thiếu hoặc không an toàn (fail closed).
public static class JwtConfigurationValidator
{
    private const int MinimumSigningKeyLength = 32;

    private static readonly string[] KnownDevelopmentKeys =
    [
        "replace-with-local-development-signing-key",
        "local-development-signing-key-change-before-production",
    ];

    public static void Validate(JwtOptions tenant, PlatformJwtOptions platform, bool isProduction)
    {
        if (string.IsNullOrWhiteSpace(tenant.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey chưa được cấu hình. Đặt key qua appsettings của môi trường hoặc biến môi trường Jwt__SigningKey.");
        }

        if (string.IsNullOrWhiteSpace(platform.SigningKey))
        {
            throw new InvalidOperationException(
                "PlatformJwt:SigningKey chưa được cấu hình. Đặt key qua appsettings của môi trường hoặc biến môi trường PlatformJwt__SigningKey.");
        }

        // Platform key luôn phải đủ dài, kể cả môi trường development.
        if (platform.SigningKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException("PlatformJwt:SigningKey cần tối thiểu 32 ký tự.");
        }

        if (string.Equals(tenant.SigningKey, platform.SigningKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PlatformJwt:SigningKey không được dùng chung với Jwt:SigningKey.");
        }

        if (string.Equals(tenant.Issuer, platform.Issuer, StringComparison.Ordinal)
            || string.Equals(tenant.Audience, platform.Audience, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Issuer và audience của PlatformJwt phải khác Jwt (tenant).");
        }

        if (string.IsNullOrWhiteSpace(platform.Issuer) || string.IsNullOrWhiteSpace(platform.Audience))
        {
            throw new InvalidOperationException("PlatformJwt:Issuer và PlatformJwt:Audience là bắt buộc.");
        }

        if (platform.AccessTokenMinutes is < 1 or > PlatformJwtOptions.MaxAccessTokenMinutes)
        {
            throw new InvalidOperationException("PlatformJwt:AccessTokenMinutes phải nằm trong khoảng 1-15 phút.");
        }

        if (!isProduction)
        {
            return;
        }

        foreach (var (name, key) in new[] { ("Jwt:SigningKey", tenant.SigningKey), ("PlatformJwt:SigningKey", platform.SigningKey) })
        {
            if (key.Length < MinimumSigningKeyLength
                || KnownDevelopmentKeys.Contains(key)
                || key.Contains("local-development", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{name} không hợp lệ cho Production: cần tối thiểu 32 ký tự và không được dùng key development mặc định.");
            }
        }
    }
}
