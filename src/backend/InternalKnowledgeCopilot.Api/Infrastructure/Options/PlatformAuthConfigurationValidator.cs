namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

// Chặn khởi động khi cấu hình chống dò mật khẩu platform không an toàn (fail closed). Rate limit đăng nhập platform
// luôn bật, không phụ thuộc RateLimiting:Enabled.
public static class PlatformAuthConfigurationValidator
{
    public const int MinFailedLoginAttempts = 1;
    public const int MaxFailedLoginAttempts = 10;
    public const int MinLockoutMinutes = 5;
    public const int MaxLockoutMinutes = 1440;
    public const int MinPlatformAuthRequestsPerMinute = 1;
    public const int MaxPlatformAuthRequestsPerMinute = 60;

    public static void Validate(PlatformAuthOptions platformAuth, RateLimitingOptions rateLimiting)
    {
        if (platformAuth.MaxFailedLoginAttempts is < MinFailedLoginAttempts or > MaxFailedLoginAttempts)
        {
            throw new InvalidOperationException(
                $"PlatformAuth:MaxFailedLoginAttempts phải nằm trong khoảng {MinFailedLoginAttempts}-{MaxFailedLoginAttempts}.");
        }

        if (platformAuth.LockoutMinutes is < MinLockoutMinutes or > MaxLockoutMinutes)
        {
            throw new InvalidOperationException(
                $"PlatformAuth:LockoutMinutes phải nằm trong khoảng {MinLockoutMinutes}-{MaxLockoutMinutes} phút.");
        }

        if (rateLimiting.PlatformAuthRequestsPerMinute is < MinPlatformAuthRequestsPerMinute or > MaxPlatformAuthRequestsPerMinute)
        {
            throw new InvalidOperationException(
                $"RateLimiting:PlatformAuthRequestsPerMinute phải nằm trong khoảng {MinPlatformAuthRequestsPerMinute}-{MaxPlatformAuthRequestsPerMinute}.");
        }
    }
}
