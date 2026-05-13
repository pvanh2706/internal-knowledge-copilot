namespace InternalKnowledgeCopilot.Api.Modules.AiSettings;

public sealed record AiProviderSettingsResponse(
    string Name,
    string BaseUrl,
    bool HasApiKey,
    string ApiKeyHeaderName,
    string ChatEndpointMode,
    string ChatModel,
    string FastModel,
    string EmbeddingProviderName,
    string EmbeddingBaseUrl,
    bool HasEmbeddingApiKey,
    string EmbeddingApiKeyHeaderName,
    string EmbeddingModel,
    int EmbeddingDimension,
    string ReasoningEffort,
    double? Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateAiProviderSettingsRequest(
    string Name,
    string BaseUrl,
    string? ApiKey,
    bool ClearApiKey,
    string ApiKeyHeaderName,
    string ChatEndpointMode,
    string ChatModel,
    string FastModel,
    string EmbeddingProviderName,
    string EmbeddingBaseUrl,
    string? EmbeddingApiKey,
    bool ClearEmbeddingApiKey,
    string EmbeddingApiKeyHeaderName,
    string EmbeddingModel,
    int EmbeddingDimension,
    string ReasoningEffort,
    double? Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds);

public sealed record TestAiProviderSettingsResponse(
    bool Success,
    string ProviderName,
    string Message);
