using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class MockAnswerGenerationServiceTests
{
    [Fact]
    public void Generate_AsksForClarification_WhenNoChunksMatch()
    {
        var service = new MockAnswerGenerationService();

        var answer = service.Generate("quy trinh thanh toan", []);

        Assert.True(answer.NeedsClarification);
        Assert.Contains("chưa tìm thấy", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }
}
