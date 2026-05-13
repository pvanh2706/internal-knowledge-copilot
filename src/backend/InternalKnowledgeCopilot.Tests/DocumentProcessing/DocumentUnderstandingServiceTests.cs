using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.DocumentProcessing;

public sealed class DocumentUnderstandingServiceTests
{
    [Fact]
    public async Task MockAnalyzeAsync_ExtractsDocumentUnderstandingMetadata()
    {
        var service = new MockDocumentUnderstandingService();
        var text = """
            Quy trinh thanh toan noi bo.
            Buoc 1: kiem tra hoa don va log nha cung cap VNPT.
            Buoc 2: duyet thanh toan tren he thong noi bo.
            Hieu luc 2026-05-13.
            """;

        var result = await service.AnalyzeAsync(
            "Quy trinh thanh toan VNPT",
            text,
            [new DocumentSection(0, "Quy trinh", 0, text.Length, text)]);

        Assert.Equal("vi", result.Language);
        Assert.Equal("procedure", result.DocumentType);
        Assert.Equal("internal", result.Sensitivity);
        Assert.Contains("VNPT", result.Entities);
        Assert.NotEmpty(result.KeyTopics);
        Assert.Contains("thanh", result.KeyTopics);
        Assert.NotNull(result.EffectiveDate);
        Assert.Contains("kiem tra hoa don", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockAnalyzeAsync_AddsQualityWarnings_ForWeakExtraction()
    {
        var service = new MockDocumentUnderstandingService();

        var result = await service.AnalyzeAsync("Short", "MÃƒÂ¬nh", []);

        Assert.Contains("document_text_short", result.QualityWarnings);
        Assert.Contains("no_section_outline_detected", result.QualityWarnings);
        Assert.Contains("possible_encoding_issue", result.QualityWarnings);
    }
}
