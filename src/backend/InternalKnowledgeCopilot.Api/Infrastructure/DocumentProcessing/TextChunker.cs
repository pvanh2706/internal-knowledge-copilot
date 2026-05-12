namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text, IReadOnlyList<DocumentSection>? sections = null);
}

public sealed class TextChunker : ITextChunker
{
    private const int TargetCharacters = 2800;
    private const int OverlapCharacters = 350;

    public IReadOnlyList<TextChunk> Chunk(string text, IReadOnlyList<DocumentSection>? sections = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = string.Join(Environment.NewLine, text.SplitLines().Select(line => line.Trim()).Where(line => !string.IsNullOrWhiteSpace(line)));
        if (sections is not null && sections.Count > 0)
        {
            return ChunkSections(sections);
        }

        if (normalized.Length <= TargetCharacters)
        {
            return [new TextChunk(0, normalized, StartOffset: 0, EndOffset: normalized.Length)];
        }

        return ChunkText(normalized, 0, null, null, 0).Chunks;
    }

    private static IReadOnlyList<TextChunk> ChunkSections(IReadOnlyList<DocumentSection> sections)
    {
        var chunks = new List<TextChunk>();
        foreach (var section in sections)
        {
            var result = ChunkText(section.Text, section.StartOffset, section.Title, section.Index, chunks.Count);
            chunks.AddRange(result.Chunks);
        }

        return chunks;
    }

    private static ChunkTextResult ChunkText(string normalized, int baseOffset, string? sectionTitle, int? sectionIndex, int firstIndex)
    {
        if (normalized.Length <= TargetCharacters)
        {
            return new ChunkTextResult([new TextChunk(firstIndex, normalized, sectionTitle, sectionIndex, baseOffset, baseOffset + normalized.Length)]);
        }

        var chunks = new List<TextChunk>();
        var start = 0;
        var index = firstIndex;
        while (start < normalized.Length)
        {
            var length = Math.Min(TargetCharacters, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var paragraphBreak = normalized.LastIndexOf(Environment.NewLine, end, length, StringComparison.Ordinal);
                if (paragraphBreak > start + TargetCharacters / 2)
                {
                    end = paragraphBreak;
                }
            }

            var chunkText = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(new TextChunk(index++, chunkText, sectionTitle, sectionIndex, baseOffset + start, baseOffset + end));
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - OverlapCharacters);
        }

        return new ChunkTextResult(chunks);
    }

    private sealed record ChunkTextResult(IReadOnlyList<TextChunk> Chunks);
}

internal static class TextChunkerStringExtensions
{
    public static string[] SplitLines(this string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }
}
