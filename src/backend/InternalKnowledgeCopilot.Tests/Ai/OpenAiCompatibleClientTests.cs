using System.Net;
using System.Text;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class OpenAiCompatibleClientTests
{
    [Fact]
    public async Task CompleteAsync_CanBeCalledTwiceWithSameHttpClient()
    {
        var handler = new StubHttpMessageHandler("""{"choices":[{"message":{"content":"ok"}}]}""");
        var client = new OpenAiCompatibleClient(new HttpClient(handler));
        var options = CreateOptions();

        var first = await client.CompleteAsync("system", "user", options);
        var second = await client.CompleteAsync("system", "user", options);

        Assert.Equal("ok", first);
        Assert.Equal("ok", second);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_CanBeCalledTwiceWithSameHttpClient()
    {
        var handler = new StubHttpMessageHandler("""{"data":[{"embedding":[0.25,0.75]}]}""");
        var client = new OpenAiCompatibleClient(new HttpClient(handler));
        var options = CreateOptions();
        options.EmbeddingProviderName = "openai";
        options.EmbeddingApiKey = "test-key";
        options.EmbeddingModel = "text-embedding-test";
        options.EmbeddingDimension = 2;

        var first = await client.CreateEmbeddingAsync("hello", options);
        var second = await client.CreateEmbeddingAsync("hello", options);

        Assert.Equal([0.25f, 0.75f], first);
        Assert.Equal([0.25f, 0.75f], second);
        Assert.Equal(2, handler.RequestCount);
    }

    private static AiProviderOptions CreateOptions()
    {
        return new AiProviderOptions
        {
            Name = "openai",
            BaseUrl = "https://example.local/v1",
            ApiKey = "test-key",
            ApiKeyHeaderName = "Authorization",
            ChatEndpointMode = "chat-completions",
            ChatModel = "chat-test",
            FastModel = "chat-test",
            EmbeddingProviderName = "mock",
            EmbeddingBaseUrl = "https://example.local/v1",
            EmbeddingModel = "mock",
            EmbeddingDimension = 64,
            ReasoningEffort = "medium",
            MaxOutputTokens = 100,
            TimeoutSeconds = 5,
        };
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount += 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
