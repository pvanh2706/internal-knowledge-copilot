namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiProviderSettingEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public required string ApiKeyHeaderName { get; set; }

    public required string ChatEndpointMode { get; set; }

    public required string ChatModel { get; set; }

    public required string FastModel { get; set; }

    public required string EmbeddingModel { get; set; }

    public int EmbeddingDimension { get; set; }

    public required string EmbeddingProviderName { get; set; }

    public required string EmbeddingBaseUrl { get; set; }

    public string? EmbeddingApiKey { get; set; }

    public required string EmbeddingApiKeyHeaderName { get; set; }

    public required string ReasoningEffort { get; set; }

    public double? Temperature { get; set; }

    public int MaxOutputTokens { get; set; }

    public int TimeoutSeconds { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public UserEntity? UpdatedByUser { get; set; }
}
