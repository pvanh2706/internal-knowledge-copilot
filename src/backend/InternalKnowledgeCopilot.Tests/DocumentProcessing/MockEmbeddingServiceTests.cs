using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.DocumentProcessing;

public sealed class MockEmbeddingServiceTests
{
    [Fact]
    public async Task CreateEmbeddingAsync_ReturnsStableNormalizedVector()
    {
        var service = new MockEmbeddingService();

        var first = await service.CreateEmbeddingAsync("payment error workflow");
        var second = await service.CreateEmbeddingAsync("payment error workflow");
        var norm = MathF.Sqrt(first.Sum(value => value * value));

        Assert.Equal(service.Dimension, first.Length);
        Assert.Equal(first, second);
        Assert.InRange(norm, 0.99f, 1.01f);
    }
}
