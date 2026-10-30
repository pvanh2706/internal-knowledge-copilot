namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    // Giới hạn theo user cho nhóm endpoint AI vì mỗi request có thể tốn chi phí LLM/embedding.
    public int AiRequestsPerMinute { get; init; } = 30;

    // Giới hạn theo integration key cho nhóm endpoint tích hợp bên thứ 3.
    public int IntegrationRequestsPerMinute { get; init; } = 120;

    // Giới hạn theo IP cho đăng nhập Platform Admin.
    public int PlatformAuthRequestsPerMinute { get; init; } = 10;
}
