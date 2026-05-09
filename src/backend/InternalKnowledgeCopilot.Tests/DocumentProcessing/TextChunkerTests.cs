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
}
