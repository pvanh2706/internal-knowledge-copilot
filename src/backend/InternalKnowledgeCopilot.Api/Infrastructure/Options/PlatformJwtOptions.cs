namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

// Cấu hình PlatformJwt tách hoàn toàn khỏi Jwt (tenant): key, issuer và audience riêng.
public sealed class PlatformJwtOptions
{
    public const string SectionName = "PlatformJwt";

    public const int MaxAccessTokenMinutes = 15;

    public string Issuer { get; init; } = "InternalKnowledgeCopilot.Platform";

    public string Audience { get; init; } = "InternalKnowledgeCopilot.PlatformAdmin";

    // Không có default: bắt buộc cấu hình qua appsettings môi trường hoặc biến môi trường PlatformJwt__SigningKey.
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = MaxAccessTokenMinutes;
}
