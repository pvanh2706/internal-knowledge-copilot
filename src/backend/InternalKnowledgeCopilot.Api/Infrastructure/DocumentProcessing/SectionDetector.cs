using System.Text.RegularExpressions;

namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface ISectionDetector
{
    IReadOnlyList<DocumentSection> Detect(string normalizedText);
}

public sealed partial class SectionDetector : ISectionDetector
{
    private const string FallbackTitle = "Document";

    public IReadOnlyList<DocumentSection> Detect(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return [];
        }

        var headings = FindHeadings(normalizedText);
        if (headings.Count == 0)
        {
            return [new DocumentSection(0, FallbackTitle, 0, normalizedText.Length, normalizedText)];
        }

        var sections = new List<DocumentSection>(headings.Count);
        for (var i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            var end = i + 1 < headings.Count ? headings[i + 1].StartOffset : normalizedText.Length;
            var text = normalizedText[heading.StartOffset..end].Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                sections.Add(new DocumentSection(sections.Count, heading.Title, heading.StartOffset, end, text));
            }
        }

        return sections.Count == 0
            ? [new DocumentSection(0, FallbackTitle, 0, normalizedText.Length, normalizedText)]
            : sections;
    }

    private static List<DetectedHeading> FindHeadings(string text)
    {
        var headings = new List<DetectedHeading>();
        var offset = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            var title = TryGetHeadingTitle(line);
            if (title is not null)
            {
                headings.Add(new DetectedHeading(offset, title));
            }

            offset += rawLine.Length + 1;
        }

        return headings;
    }

    private static string? TryGetHeadingTitle(string line)
    {
        if (line.Length is < 3 or > 160)
        {
            return null;
        }

        var markdown = MarkdownHeadingRegex().Match(line);
        if (markdown.Success)
        {
            return markdown.Groups["title"].Value.Trim();
        }

        var numbered = NumberedHeadingRegex().Match(line);
        if (numbered.Success)
        {
            var marker = numbered.Groups["marker"].Value.Trim();
            var title = numbered.Groups["title"].Value.Trim();
            return LooksLikeNumberedHeadingTitle(marker, title) ? title : null;
        }

        if (LooksLikeVietnameseHeading(line))
        {
            return line;
        }

        return null;
    }

    private static bool LooksLikeVietnameseHeading(string line)
    {
        var lower = line.ToLowerInvariant();
        var prefixes = new[]
        {
            "mục đích",
            "phạm vi",
            "đối tượng",
            "nội dung",
            "quy trình",
            "các bước",
            "lưu ý",
            "faq",
            "câu hỏi",
            "nguồn",
        };

        return prefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeNumberedHeadingTitle(string marker, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.EndsWith('.') || title.EndsWith('!') || title.EndsWith('?'))
        {
            return false;
        }

        if (marker.Contains('.', StringComparison.Ordinal) || RomanNumeralRegex().IsMatch(marker))
        {
            return true;
        }

        var wordCount = title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return wordCount <= 4 || LooksLikeVietnameseHeading(title);
    }

    [GeneratedRegex(@"^#{1,6}\s+(?<title>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"^(?<marker>\d+(?:\.\d+)*|[IVX]+)[\.\)]\s+(?<title>.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NumberedHeadingRegex();

    [GeneratedRegex(@"^[IVX]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RomanNumeralRegex();

    private sealed record DetectedHeading(int StartOffset, string Title);
}

public sealed record DocumentSection(int Index, string Title, int StartOffset, int EndOffset, string Text);
