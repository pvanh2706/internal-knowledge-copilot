namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class AiProviderOptions
{
    public const string SectionName = "AiProvider";

    public string Name { get; set; } = "mock";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = "Authorization";

    public string ChatEndpointMode { get; set; } = "chat-completions";

    public string ChatModel { get; set; } = "gpt-5.5";

    public string FastModel { get; set; } = "gpt-5.5";

    public string EmbeddingModel { get; set; } = "text-embedding-3-large";

    public int EmbeddingDimension { get; set; } = 3072;

    public string EmbeddingProviderName { get; set; } = "openai-compatible";

    public string EmbeddingBaseUrl { get; set; } = "https://api.openai.com/v1";

    public string EmbeddingApiKey { get; set; } = string.Empty;

    public string EmbeddingApiKeyHeaderName { get; set; } = "Authorization";

    public string ReasoningEffort { get; set; } = "medium";

    public double? Temperature { get; set; } = 0.2;

    public int MaxOutputTokens { get; set; } = 2500;

    public int TimeoutSeconds { get; set; } = 60;
}
