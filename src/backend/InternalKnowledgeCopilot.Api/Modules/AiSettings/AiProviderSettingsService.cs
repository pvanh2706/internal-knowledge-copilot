using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Modules.AiSettings;

public interface IAiProviderSettingsService
{
    AiProviderOptions GetCurrent();

    Task<AiProviderOptions> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<AiProviderSettingsResponse> GetForAdminAsync(CancellationToken cancellationToken = default);

    Task<AiProviderSettingsResponse> UpdateAsync(
        Guid actorUserId,
        UpdateAiProviderSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<TestAiProviderSettingsResponse> TestAsync(CancellationToken cancellationToken = default);
}

public sealed class AiProviderSettingsService(
    AppDbContext dbContext,
    IOptions<AiProviderOptions> fallbackOptions,
    OpenAiCompatibleClient openAiClient,
    IAuditLogService auditLogService) : IAiProviderSettingsService
{
    private const int SingletonId = 1;

    public AiProviderOptions GetCurrent()
    {
        var entity = dbContext.AiProviderSettings.AsNoTracking().FirstOrDefault(setting => setting.Id == SingletonId);
        return MergeWithFallback(entity);
    }

    public async Task<AiProviderOptions> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AiProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Id == SingletonId, cancellationToken);

        return MergeWithFallback(entity);
    }

    public async Task<AiProviderSettingsResponse> GetForAdminAsync(CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AiProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Id == SingletonId, cancellationToken);

        var options = MergeWithFallback(entity);
        return ToResponse(options, entity?.UpdatedAt, HasApiKey(options), HasEmbeddingApiKey(options));
    }

    public async Task<AiProviderSettingsResponse> UpdateAsync(
        Guid actorUserId,
        UpdateAiProviderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var existing = await dbContext.AiProviderSettings
            .FirstOrDefaultAsync(setting => setting.Id == SingletonId, cancellationToken);

        if (existing is null)
        {
            existing = new AiProviderSettingEntity
            {
                Id = SingletonId,
                Name = normalized.Name,
                BaseUrl = normalized.BaseUrl,
                ApiKey = normalized.ApiKey,
                ApiKeyHeaderName = normalized.ApiKeyHeaderName,
                ChatEndpointMode = normalized.ChatEndpointMode,
                ChatModel = normalized.ChatModel,
                FastModel = normalized.FastModel,
                EmbeddingProviderName = normalized.EmbeddingProviderName,
                EmbeddingBaseUrl = normalized.EmbeddingBaseUrl,
                EmbeddingApiKey = normalized.EmbeddingApiKey,
                EmbeddingApiKeyHeaderName = normalized.EmbeddingApiKeyHeaderName,
                EmbeddingModel = normalized.EmbeddingModel,
                EmbeddingDimension = normalized.EmbeddingDimension,
                ReasoningEffort = normalized.ReasoningEffort,
                Temperature = normalized.Temperature,
                MaxOutputTokens = normalized.MaxOutputTokens,
                TimeoutSeconds = normalized.TimeoutSeconds,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedByUserId = actorUserId,
            };
            dbContext.AiProviderSettings.Add(existing);
        }
        else
        {
            existing.Name = normalized.Name;
            existing.BaseUrl = normalized.BaseUrl;
            if (normalized.ClearApiKey)
            {
                existing.ApiKey = null;
            }
            else if (!string.IsNullOrWhiteSpace(normalized.ApiKey))
            {
                existing.ApiKey = normalized.ApiKey;
            }

            existing.ApiKeyHeaderName = normalized.ApiKeyHeaderName;
            existing.ChatEndpointMode = normalized.ChatEndpointMode;
            existing.ChatModel = normalized.ChatModel;
            existing.FastModel = normalized.FastModel;
            existing.EmbeddingProviderName = normalized.EmbeddingProviderName;
            existing.EmbeddingBaseUrl = normalized.EmbeddingBaseUrl;
            if (normalized.ClearEmbeddingApiKey)
            {
                existing.EmbeddingApiKey = null;
            }
            else if (!string.IsNullOrWhiteSpace(normalized.EmbeddingApiKey))
            {
                existing.EmbeddingApiKey = normalized.EmbeddingApiKey;
            }

            existing.EmbeddingApiKeyHeaderName = normalized.EmbeddingApiKeyHeaderName;
            existing.EmbeddingModel = normalized.EmbeddingModel;
            existing.EmbeddingDimension = normalized.EmbeddingDimension;
            existing.ReasoningEffort = normalized.ReasoningEffort;
            existing.Temperature = normalized.Temperature;
            existing.MaxOutputTokens = normalized.MaxOutputTokens;
            existing.TimeoutSeconds = normalized.TimeoutSeconds;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedByUserId = actorUserId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            actorUserId,
            "AiProviderSettingsUpdated",
            "AiProviderSettings",
            null,
            new
            {
                existing.Name,
                existing.BaseUrl,
                existing.ChatEndpointMode,
                existing.ChatModel,
                existing.EmbeddingProviderName,
                existing.EmbeddingBaseUrl,
                existing.EmbeddingModel,
                existing.EmbeddingDimension
            },
            cancellationToken);

        var options = MergeWithFallback(existing);
        return ToResponse(options, existing.UpdatedAt, HasApiKey(options), HasEmbeddingApiKey(options));
    }

    public async Task<TestAiProviderSettingsResponse> TestAsync(CancellationToken cancellationToken = default)
    {
        var options = await GetCurrentAsync(cancellationToken);
        if (IsMock(options))
        {
            return new TestAiProviderSettingsResponse(true, options.Name, "Mock provider is active. No external LLM API call was made.");
        }

        try
        {
            if (!UsesMockEmbeddings(options))
            {
                var embedding = await openAiClient.CreateEmbeddingAsync("health check", options, cancellationToken);
                if (embedding.Length == 0)
                {
                    return new TestAiProviderSettingsResponse(false, options.Name, "Provider returned an empty embedding.");
                }
            }

            var response = await openAiClient.CompleteAsync(
                "Return JSON only.",
                """Return exactly {"ok":true} and no extra text.""",
                options,
                cancellationToken);

            return response.Contains("ok", StringComparison.OrdinalIgnoreCase)
                ? new TestAiProviderSettingsResponse(true, options.Name, IsAnthropic(options)
                    ? "Claude Messages API responded successfully. Embedding provider test also passed unless it is set to mock."
                    : "LLM API and embedding API responded successfully.")
                : new TestAiProviderSettingsResponse(true, options.Name, "Provider responded, but test response did not contain the expected JSON marker.");
        }
        catch (Exception ex)
        {
            return new TestAiProviderSettingsResponse(false, options.Name, ex.Message);
        }
    }

    private AiProviderOptions MergeWithFallback(AiProviderSettingEntity? entity)
    {
        var fallback = fallbackOptions.Value;
        if (entity is null)
        {
            return Clone(fallback);
        }

        return new AiProviderOptions
        {
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            ApiKey = entity.ApiKey ?? string.Empty,
            ApiKeyHeaderName = entity.ApiKeyHeaderName,
            ChatEndpointMode = entity.ChatEndpointMode,
            ChatModel = entity.ChatModel,
            FastModel = entity.FastModel,
            EmbeddingProviderName = string.IsNullOrWhiteSpace(entity.EmbeddingProviderName) ? fallback.EmbeddingProviderName : entity.EmbeddingProviderName,
            EmbeddingBaseUrl = string.IsNullOrWhiteSpace(entity.EmbeddingBaseUrl) ? fallback.EmbeddingBaseUrl : entity.EmbeddingBaseUrl,
            EmbeddingApiKey = entity.EmbeddingApiKey ?? string.Empty,
            EmbeddingApiKeyHeaderName = string.IsNullOrWhiteSpace(entity.EmbeddingApiKeyHeaderName) ? fallback.EmbeddingApiKeyHeaderName : entity.EmbeddingApiKeyHeaderName,
            EmbeddingModel = entity.EmbeddingModel,
            EmbeddingDimension = entity.EmbeddingDimension,
            ReasoningEffort = entity.ReasoningEffort,
            Temperature = entity.Temperature,
            MaxOutputTokens = entity.MaxOutputTokens,
            TimeoutSeconds = entity.TimeoutSeconds,
        };
    }

    private static AiProviderOptions Clone(AiProviderOptions options)
    {
        return new AiProviderOptions
        {
            Name = options.Name,
            BaseUrl = options.BaseUrl,
            ApiKey = options.ApiKey,
            ApiKeyHeaderName = options.ApiKeyHeaderName,
            ChatEndpointMode = options.ChatEndpointMode,
            ChatModel = options.ChatModel,
            FastModel = options.FastModel,
            EmbeddingProviderName = options.EmbeddingProviderName,
            EmbeddingBaseUrl = options.EmbeddingBaseUrl,
            EmbeddingApiKey = options.EmbeddingApiKey,
            EmbeddingApiKeyHeaderName = options.EmbeddingApiKeyHeaderName,
            EmbeddingModel = options.EmbeddingModel,
            EmbeddingDimension = options.EmbeddingDimension,
            ReasoningEffort = options.ReasoningEffort,
            Temperature = options.Temperature,
            MaxOutputTokens = options.MaxOutputTokens,
            TimeoutSeconds = options.TimeoutSeconds,
        };
    }

    private static UpdateAiProviderSettingsRequest Normalize(UpdateAiProviderSettingsRequest request)
    {
        var name = CleanRequired(request.Name, "name_required");
        var allowedNames = new[] { "mock", "openai", "openai-compatible", "local", "azure-openai", "anthropic", "claude" };
        if (!allowedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("invalid_provider_name");
        }

        var baseUrl = CleanRequired(request.BaseUrl, "base_url_required").TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_base_url");
        }

        var endpointMode = CleanRequired(request.ChatEndpointMode, "chat_endpoint_mode_required").ToLowerInvariant();
        if (endpointMode is not ("chat-completions" or "responses" or "messages"))
        {
            throw new ArgumentException("invalid_chat_endpoint_mode");
        }

        var headerName = string.IsNullOrWhiteSpace(request.ApiKeyHeaderName)
            ? "Authorization"
            : request.ApiKeyHeaderName.Trim();

        var embeddingProviderName = string.IsNullOrWhiteSpace(request.EmbeddingProviderName)
            ? "mock"
            : request.EmbeddingProviderName.Trim();
        var allowedEmbeddingNames = new[] { "mock", "openai", "openai-compatible", "local", "azure-openai" };
        if (!allowedEmbeddingNames.Contains(embeddingProviderName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("invalid_embedding_provider_name");
        }

        var embeddingBaseUrl = CleanRequired(request.EmbeddingBaseUrl, "embedding_base_url_required").TrimEnd('/');
        if (!Uri.TryCreate(embeddingBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("invalid_embedding_base_url");
        }

        var embeddingHeaderName = string.IsNullOrWhiteSpace(request.EmbeddingApiKeyHeaderName)
            ? "Authorization"
            : request.EmbeddingApiKeyHeaderName.Trim();

        if (request.EmbeddingDimension is < 1 or > 20000)
        {
            throw new ArgumentException("invalid_embedding_dimension");
        }

        if (request.MaxOutputTokens is < 1 or > 100000)
        {
            throw new ArgumentException("invalid_max_output_tokens");
        }

        if (request.TimeoutSeconds is < 1 or > 600)
        {
            throw new ArgumentException("invalid_timeout_seconds");
        }

        if (request.Temperature is < 0 or > 2)
        {
            throw new ArgumentException("invalid_temperature");
        }

        return request with
        {
            Name = name,
            BaseUrl = baseUrl,
            ApiKey = request.ApiKey?.Trim(),
            ApiKeyHeaderName = headerName,
            ChatEndpointMode = endpointMode,
            ChatModel = CleanRequired(request.ChatModel, "chat_model_required"),
            FastModel = string.IsNullOrWhiteSpace(request.FastModel) ? CleanRequired(request.ChatModel, "chat_model_required") : request.FastModel.Trim(),
            EmbeddingProviderName = embeddingProviderName,
            EmbeddingBaseUrl = embeddingBaseUrl,
            EmbeddingApiKey = request.EmbeddingApiKey?.Trim(),
            EmbeddingApiKeyHeaderName = embeddingHeaderName,
            EmbeddingModel = CleanRequired(request.EmbeddingModel, "embedding_model_required"),
            ReasoningEffort = string.IsNullOrWhiteSpace(request.ReasoningEffort) ? "medium" : request.ReasoningEffort.Trim(),
        };
    }

    private static string CleanRequired(string value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorCode);
        }

        return value.Trim();
    }

    private static bool IsMock(AiProviderOptions options)
    {
        return string.Equals(options.Name, "mock", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnthropic(AiProviderOptions options)
    {
        return string.Equals(options.Name, "anthropic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.Name, "claude", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesMockEmbeddings(AiProviderOptions options)
    {
        return string.Equals(options.EmbeddingProviderName, "mock", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasApiKey(AiProviderOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    private static bool HasEmbeddingApiKey(AiProviderOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.EmbeddingApiKey);
    }

    private static AiProviderSettingsResponse ToResponse(
        AiProviderOptions options,
        DateTimeOffset? updatedAt,
        bool hasApiKey,
        bool hasEmbeddingApiKey)
    {
        return new AiProviderSettingsResponse(
            options.Name,
            options.BaseUrl,
            hasApiKey,
            options.ApiKeyHeaderName,
            options.ChatEndpointMode,
            options.ChatModel,
            options.FastModel,
            options.EmbeddingProviderName,
            options.EmbeddingBaseUrl,
            hasEmbeddingApiKey,
            options.EmbeddingApiKeyHeaderName,
            options.EmbeddingModel,
            options.EmbeddingDimension,
            options.ReasoningEffort,
            options.Temperature,
            options.MaxOutputTokens,
            options.TimeoutSeconds,
            updatedAt);
    }
}
