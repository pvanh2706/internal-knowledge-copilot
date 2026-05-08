namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "InternalKnowledgeCopilot";

    public string Audience { get; init; } = "InternalKnowledgeCopilot";

    public string SigningKey { get; init; } = "replace-with-local-development-signing-key";

    public int AccessTokenMinutes { get; init; } = 120;
}
