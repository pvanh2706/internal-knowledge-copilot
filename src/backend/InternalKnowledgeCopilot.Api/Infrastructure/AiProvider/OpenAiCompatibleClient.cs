using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public sealed class OpenAiCompatibleClient(HttpClient httpClient)
{
    public async Task<float[]> CreateEmbeddingAsync(
        string text,
        AiProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        EnsureEmbeddingApiKeyIfNeeded(options);
        if (IsMockEmbeddingProvider(options))
        {
            throw new InvalidOperationException("Mock embedding provider does not call an external embeddings endpoint.");
        }

        var payload = new EmbeddingRequest(options.EmbeddingModel, text);
        var response = await PostEmbeddingAsJsonAsync(options, "embeddings", payload, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var embeddingResponse = await JsonSerializer.DeserializeAsync<EmbeddingResponse>(stream, cancellationToken: cancellationToken);
        var embedding = embeddingResponse?.Data?.FirstOrDefault()?.Embedding;
        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException("AI provider did not return an embedding.");
        }

        return embedding;
    }

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        AiProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        EnsureChatApiKeyIfNeeded(options);

        if (IsAnthropic(options))
        {
            return CompleteWithAnthropicMessagesAsync(systemPrompt, userPrompt, options, cancellationToken);
        }

        return string.Equals(options.ChatEndpointMode, "responses", StringComparison.OrdinalIgnoreCase)
            ? CompleteWithResponsesAsync(systemPrompt, userPrompt, options, cancellationToken)
            : CompleteWithChatCompletionsAsync(systemPrompt, userPrompt, options, cancellationToken);
    }

    private async Task<string> CompleteWithAnthropicMessagesAsync(
        string systemPrompt,
        string userPrompt,
        AiProviderOptions options,
        CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.ChatModel,
            ["system"] = systemPrompt,
            ["max_tokens"] = options.MaxOutputTokens,
            ["messages"] = new object[]
            {
                new { role = "user", content = userPrompt },
            },
        };

        if (options.Temperature is not null)
        {
            request["temperature"] = options.Temperature.Value;
        }

        var response = await PostAsJsonAsync(options, "messages", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Anthropic provider did not return message content.");
        }

        var texts = content
            .EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type)
                && string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase)
                && item.TryGetProperty("text", out _))
            .Select(item => item.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        if (texts.Length == 0)
        {
            throw new InvalidOperationException("Anthropic provider did not return text content.");
        }

        return string.Join(Environment.NewLine, texts);
    }

    private async Task<string> CompleteWithChatCompletionsAsync(
        string systemPrompt,
        string userPrompt,
        AiProviderOptions options,
        CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.ChatModel,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            ["max_tokens"] = options.MaxOutputTokens,
        };

        if (options.Temperature is not null)
        {
            request["temperature"] = options.Temperature.Value;
        }

        var response = await PostAsJsonAsync(options, "chat/completions", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            var firstChoice = choices.EnumerateArray().FirstOrDefault();
            if (firstChoice.ValueKind == JsonValueKind.Object
                && firstChoice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                var text = ReadTextContent(content);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException("AI provider did not return chat completion text.");
    }

    private async Task<string> CompleteWithResponsesAsync(
        string systemPrompt,
        string userPrompt,
        AiProviderOptions options,
        CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.ChatModel,
            ["instructions"] = systemPrompt,
            ["input"] = userPrompt,
            ["max_output_tokens"] = options.MaxOutputTokens,
        };

        if (options.Temperature is not null)
        {
            request["temperature"] = options.Temperature.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ReasoningEffort))
        {
            request["reasoning"] = new { effort = options.ReasoningEffort };
        }

        var response = await PostAsJsonAsync(options, "responses", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            var text = outputText.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            var texts = new List<string>();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            texts.Add(text);
                        }
                    }
                }
            }

            if (texts.Count > 0)
            {
                return string.Join(Environment.NewLine, texts);
            }
        }

        throw new InvalidOperationException("AI provider did not return response text.");
    }

    private async Task<HttpResponseMessage> PostAsJsonAsync(
        AiProviderOptions options,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(options, relativePath))
        {
            Content = JsonContent.Create(payload),
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            if (IsAnthropic(options))
            {
                request.Headers.Add("x-api-key", options.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (string.Equals(options.ApiKeyHeaderName, "api-key", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Add("api-key", options.ApiKey);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostEmbeddingAsJsonAsync(
        AiProviderOptions options,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEmbeddingUri(options, relativePath))
        {
            Content = JsonContent.Create(payload),
        };

        if (!string.IsNullOrWhiteSpace(options.EmbeddingApiKey))
        {
            if (string.Equals(options.EmbeddingApiKeyHeaderName, "api-key", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Add("api-key", options.EmbeddingApiKey);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.EmbeddingApiKey);
            }
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static Uri BuildUri(AiProviderOptions options, string relativePath)
    {
        var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal) ? options.BaseUrl : options.BaseUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }

    private static Uri BuildEmbeddingUri(AiProviderOptions options, string relativePath)
    {
        var baseUrl = options.EmbeddingBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.EmbeddingBaseUrl
            : options.EmbeddingBaseUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }

    private static bool IsAnthropic(AiProviderOptions options)
    {
        return options.Name.Equals("anthropic", StringComparison.OrdinalIgnoreCase)
            || options.Name.Equals("claude", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMockEmbeddingProvider(AiProviderOptions options)
    {
        return options.EmbeddingProviderName.Equals("mock", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureChatApiKeyIfNeeded(AiProviderOptions options)
    {
        var providerName = options.Name.Trim();
        if (!string.Equals(providerName, "local", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("AiProvider:ApiKey is required for the configured AI provider.");
        }
    }

    private static void EnsureEmbeddingApiKeyIfNeeded(AiProviderOptions options)
    {
        var embeddingProviderName = options.EmbeddingProviderName.Trim();
        if (!string.Equals(embeddingProviderName, "mock", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(embeddingProviderName, "local", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.EmbeddingApiKey))
        {
            throw new InvalidOperationException("AiProvider:EmbeddingApiKey is required for the configured embedding provider.");
        }
    }

    private static string ReadTextContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            content.EnumerateArray()
                .Where(item => item.TryGetProperty("text", out _))
                .Select(item => item.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (responseBody.Length > 1000)
        {
            responseBody = responseBody[..1000] + "...";
        }

        throw new InvalidOperationException($"AI provider request failed with HTTP {(int)response.StatusCode}: {responseBody}");
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record EmbeddingResponse([property: JsonPropertyName("data")] EmbeddingData[] Data);

    private sealed record EmbeddingData([property: JsonPropertyName("embedding")] float[] Embedding);
}
