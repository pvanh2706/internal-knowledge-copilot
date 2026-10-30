namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class PlatformAuthOptions
{
    public const string SectionName = "PlatformAuth";

    public int MaxFailedLoginAttempts { get; init; } = 5;

    public int LockoutMinutes { get; init; } = 15;
}
