using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class MockAnswerGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_AsksForClarification_WhenNoChunksMatch()
    {
        var service = new MockAnswerGenerationService();

        var answer = await service.GenerateAsync("quy trinh thanh toan", []);

        Assert.True(answer.NeedsClarification);
        Assert.Equal("low", answer.Confidence);
        Assert.NotEmpty(answer.MissingInformation);
        Assert.Empty(answer.CitedSourceIds);
        Assert.Contains("chua tim thay", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsGroundedMetadata_WhenChunksMatch()
    {
        var service = new MockAnswerGenerationService();

        var answer = await service.GenerateAsync(
            "payment error",
            [
                new RetrievedKnowledgeChunk(
                    KnowledgeSourceType.Document,
                    "source-1",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    Guid.NewGuid(),
                    "folder",
                    "Payment",
                    "/Finance",
                    "Troubleshooting",
                    1,
                    "payment error must be checked in the approved payment source",
                    0.1),
            ]);

        Assert.False(answer.NeedsClarification);
        Assert.Equal("low", answer.Confidence);
        Assert.Empty(answer.MissingInformation);
        Assert.Equal(["source-1"], answer.CitedSourceIds);
    }
}
