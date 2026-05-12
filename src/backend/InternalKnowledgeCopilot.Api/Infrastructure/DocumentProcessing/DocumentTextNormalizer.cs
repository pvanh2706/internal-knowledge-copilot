using System.Text;
using System.Text.Json;

namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface IDocumentTextNormalizer
{
    DocumentTextNormalizationResult Normalize(string text);
}

public sealed class DocumentTextNormalizer : IDocumentTextNormalizer
{
    public DocumentTextNormalizationResult Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentTextNormalizationResult(string.Empty, ["empty_text"]);
        }

        var warnings = new List<string>();
        var normalized = text.Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        if (normalized.Contains('\uFFFD', StringComparison.Ordinal))
        {
            warnings.Add("replacement_character_found");
        }

        if (LooksLikeMojibake(normalized))
        {
            warnings.Add("possible_encoding_issue");
        }

        var lines = normalized
            .Split('\n')
            .Select(line => RemoveControlCharacters(line).TrimEnd())
            .ToList();

        var compacted = new List<string>(lines.Count);
        var blankCount = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blankCount++;
                if (blankCount <= 1)
                {
                    compacted.Add(string.Empty);
                }

                continue;
            }

            blankCount = 0;
            compacted.Add(CollapseInlineWhitespace(line.Trim()));
        }

        var result = string.Join('\n', compacted).Trim();
        if (result.Length < text.Trim().Length / 2)
        {
            warnings.Add("text_shrank_significantly_after_normalization");
        }

        return new DocumentTextNormalizationResult(result, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool LooksLikeMojibake(string text)
    {
        var markers = new[] { "Ã", "Â", "Ä", "Æ", "áº", "á»" };
        return markers.Count(marker => text.Contains(marker, StringComparison.Ordinal)) >= 2;
    }

    private static string RemoveControlCharacters(string value)
    {
        return new string(value.Where(character => !char.IsControl(character) || character == '\t').ToArray());
    }

    private static string CollapseInlineWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var character in value)
        {
            if (character is ' ' or '\t')
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(character);
            lastWasSpace = false;
        }

        return builder.ToString();
    }
}

public sealed record DocumentTextNormalizationResult(string Text, IReadOnlyList<string> Warnings)
{
    public string WarningsJson => JsonSerializer.Serialize(Warnings);
}
