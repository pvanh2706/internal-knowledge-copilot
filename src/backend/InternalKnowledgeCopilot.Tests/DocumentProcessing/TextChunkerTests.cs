using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.DocumentProcessing;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_ReturnsSingleChunk_ForShortText()
    {
        var chunker = new TextChunker();

        var chunks = chunker.Chunk("Hello document processing.");

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Index);
    }

    [Fact]
    public void Chunk_ReturnsEmpty_ForBlankText()
    {
        var chunker = new TextChunker();

        var chunks = chunker.Chunk("   ");

        Assert.Empty(chunks);
    }

    [Fact]
    public void SectionDetector_DetectsMarkdownAndNumberedHeadings()
    {
        var detector = new SectionDetector();

        var sections = detector.Detect("""
            # Overview
            Intro text.

            1. Setup
            Step one.
            """);

        Assert.Equal(2, sections.Count);
        Assert.Equal("Overview", sections[0].Title);
        Assert.Equal("Setup", sections[1].Title);
    }

    [Fact]
    public void SectionDetector_DoesNotTreatOrderedListItemsAsHeadings()
    {
        var detector = new SectionDetector();

        var sections = detector.Detect("""
            # Quy trinh xin nghi phep

            ## Cac buoc
            1. Nhan vien tao yeu cau nghi phep tren he thong HRM.
            2. Quan ly truc tiep phe duyet yeu cau.
            3. Bo phan Nhan su kiem tra so ngay phep con lai.
            """);

        Assert.Equal(2, sections.Count);
        Assert.Equal("Quy trinh xin nghi phep", sections[0].Title);
        Assert.Equal("Cac buoc", sections[1].Title);
        Assert.Contains("Bo phan Nhan su", sections[1].Text);
    }

    [Fact]
    public void Chunk_PreservesSectionMetadata()
    {
        var detector = new SectionDetector();
        var chunker = new TextChunker();
        var text = """
            # Quy trình thanh toán
            Kiểm tra chứng từ và tạo phiếu chi.

            # Lưu ý quan trọng
            Không duyệt nếu thiếu hóa đơn.
            """;

        var chunks = chunker.Chunk(text, detector.Detect(text));

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Quy trình thanh toán", chunks[0].SectionTitle);
        Assert.Equal("Lưu ý quan trọng", chunks[1].SectionTitle);
        Assert.NotNull(chunks[0].StartOffset);
        Assert.NotNull(chunks[0].EndOffset);
    }

    [Fact]
    public void Normalize_CompactsWhitespaceAndWarnsOnPossibleMojibake()
    {
        var normalizer = new DocumentTextNormalizer();

        var result = normalizer.Normalize("MÃ¬nh   chÆ°a\r\n\r\n\r\n  tÃ¬m tháº¥y");

        Assert.Contains("possible_encoding_issue", result.Warnings);
        Assert.DoesNotContain("\r", result.Text);
        Assert.DoesNotContain("   ", result.Text);
    }
}
