using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.DocumentProcessing;

public sealed class MockEmbeddingServiceTests
{
    [Fact]
    public void CreateEmbedding_ReturnsStableNormalizedVector()
    {
        var service = new MockEmbeddingService();

        var first = service.CreateEmbedding("payment error workflow");
        var second = service.CreateEmbedding("payment error workflow");
        var norm = MathF.Sqrt(first.Sum(value => value * value));

        Assert.Equal(service.Dimension, first.Length);
        Assert.Equal(first, second);
        Assert.InRange(norm, 0.99f, 1.01f);
    }
}
