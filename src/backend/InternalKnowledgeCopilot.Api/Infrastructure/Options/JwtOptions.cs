namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "InternalKnowledgeCopilot";

    public string Audience { get; init; } = "InternalKnowledgeCopilot";

    // Không có default: key bắt buộc cấu hình qua appsettings môi trường hoặc biến môi trường Jwt__SigningKey.
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 120;
}
