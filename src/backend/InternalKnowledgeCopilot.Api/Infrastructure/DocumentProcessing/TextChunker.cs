namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text);
}

public sealed class TextChunker : ITextChunker
{
    private const int TargetCharacters = 2800;
    private const int OverlapCharacters = 350;

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = string.Join(Environment.NewLine, text.SplitLines().Select(line => line.Trim()).Where(line => !string.IsNullOrWhiteSpace(line)));
        if (normalized.Length <= TargetCharacters)
        {
            return [new TextChunk(0, normalized)];
        }

        var chunks = new List<TextChunk>();
        var start = 0;
        var index = 0;
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
                chunks.Add(new TextChunk(index++, chunkText));
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - OverlapCharacters);
        }

        return chunks;
    }
}

internal static class TextChunkerStringExtensions
{
    public static string[] SplitLines(this string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }
}
