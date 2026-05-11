using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public sealed class OpenAiCompatibleClient(HttpClient httpClient, IOptions<AiProviderOptions> options)
{
    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyIfNeeded();

        var payload = new EmbeddingRequest(options.Value.EmbeddingModel, text);
        var response = await httpClient.PostAsJsonAsync("embeddings", payload, cancellationToken);
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

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyIfNeeded();

        return string.Equals(options.Value.ChatEndpointMode, "responses", StringComparison.OrdinalIgnoreCase)
            ? CompleteWithResponsesAsync(systemPrompt, userPrompt, cancellationToken)
            : CompleteWithChatCompletionsAsync(systemPrompt, userPrompt, cancellationToken);
    }

    private async Task<string> CompleteWithChatCompletionsAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.Value.ChatModel,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            ["max_tokens"] = options.Value.MaxOutputTokens,
        };

        if (options.Value.Temperature is not null)
        {
            request["temperature"] = options.Value.Temperature.Value;
        }

        var response = await httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
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

    private async Task<string> CompleteWithResponsesAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.Value.ChatModel,
            ["instructions"] = systemPrompt,
            ["input"] = userPrompt,
            ["max_output_tokens"] = options.Value.MaxOutputTokens,
        };

        if (options.Value.Temperature is not null)
        {
            request["temperature"] = options.Value.Temperature.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Value.ReasoningEffort))
        {
            request["reasoning"] = new { effort = options.Value.ReasoningEffort };
        }

        var response = await httpClient.PostAsJsonAsync("responses", request, cancellationToken);
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

    private void EnsureApiKeyIfNeeded()
    {
        var providerName = options.Value.Name.Trim();
        if (!string.Equals(providerName, "local", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new InvalidOperationException("AiProvider:ApiKey is required for the configured AI provider.");
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
