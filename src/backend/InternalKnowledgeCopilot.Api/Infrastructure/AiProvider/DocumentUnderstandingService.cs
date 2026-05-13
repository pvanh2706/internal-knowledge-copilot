using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IDocumentUnderstandingService
{
    Task<DocumentUnderstandingResult> AnalyzeAsync(
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default);
}

public sealed class MockDocumentUnderstandingService : IDocumentUnderstandingService
{
    public Task<DocumentUnderstandingResult> AnalyzeAsync(
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DocumentUnderstandingHeuristics.Analyze(title, normalizedText, sections));
    }
}

public sealed class OpenAiCompatibleDocumentUnderstandingService(OpenAiCompatibleClient client, IAiProviderSettingsService settingsService) : IDocumentUnderstandingService
{
    private const int MaxSourceCharacters = 12000;

    public async Task<DocumentUnderstandingResult> AnalyzeAsync(
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        var trimmedSource = normalizedText.Length <= MaxSourceCharacters
            ? normalizedText
            : normalizedText[..MaxSourceCharacters].TrimEnd() + "...";
        var outline = sections.Count == 0
            ? "- No section outline detected."
            : string.Join(Environment.NewLine, sections.Take(30).Select(section => $"- {section.Index}. {section.Title}"));

        var systemPrompt = """
            You analyze approved internal documents for a knowledge base.
            Use only the provided document title, outline, and text.
            Return JSON only with this schema:
            {
              "language": "vi|en|unknown",
              "documentType": "pricing|policy|procedure|technical|contract|faq|unknown",
              "summary": "string",
              "keyTopics": ["string"],
              "entities": ["string"],
              "effectiveDate": "ISO-8601 date string or null",
              "sensitivity": "normal|internal|confidential",
              "qualityWarnings": ["string"]
            }
            Do not invent dates, owners, policies, prices, or entities.
            If a field is not explicit, use null/unknown and add a warning when useful.
            """;

        var userPrompt = $"""
            Title:
            {title}

            Section outline:
            {outline}

            Text:
            {trimmedSource}
            """;

        var options = await settingsService.GetCurrentAsync(cancellationToken);
        var raw = await client.CompleteAsync(systemPrompt, userPrompt, options, cancellationToken);
        if (TryParse(raw, title, normalizedText, sections, out var result))
        {
            return result;
        }

        var repairPrompt = $"""
            The previous output was not valid JSON for the required document understanding schema.

            Previous output:
            {raw}

            Return valid JSON only for the original request:
            {userPrompt}
            """;

        var repaired = await client.CompleteAsync(systemPrompt, repairPrompt, options, cancellationToken);
        return TryParse(repaired, title, normalizedText, sections, out var repairedResult)
            ? repairedResult
            : DocumentUnderstandingHeuristics.Analyze(title, normalizedText, sections, ["ai_understanding_invalid_json"]);
    }

    private static bool TryParse(
        string raw,
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        out DocumentUnderstandingResult result)
    {
        result = null!;
        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            return false;
        }

        DocumentUnderstandingJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<DocumentUnderstandingJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null)
        {
            return false;
        }

        var fallback = DocumentUnderstandingHeuristics.Analyze(title, normalizedText, sections);
        result = new DocumentUnderstandingResult(
            NormalizeLanguage(parsed.Language, fallback.Language),
            NormalizeDocumentType(parsed.DocumentType, fallback.DocumentType),
            CleanText(parsed.Summary, fallback.Summary, 1800),
            CleanList(parsed.KeyTopics).DefaultIfEmpty().FirstOrDefault() is null ? fallback.KeyTopics : CleanList(parsed.KeyTopics),
            CleanList(parsed.Entities),
            ParseDate(parsed.EffectiveDate),
            NormalizeSensitivity(parsed.Sensitivity, fallback.Sensitivity),
            CleanList(parsed.QualityWarnings));
        return true;
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string NormalizeLanguage(string? value, string fallback)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "vi" => "vi",
            "en" => "en",
            "unknown" => "unknown",
            _ => fallback,
        };
    }

    private static string NormalizeDocumentType(string? value, string fallback)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "pricing" => "pricing",
            "policy" => "policy",
            "procedure" => "procedure",
            "technical" => "technical",
            "contract" => "contract",
            "faq" => "faq",
            "unknown" => "unknown",
            _ => fallback,
        };
    }

    private static string NormalizeSensitivity(string? value, string fallback)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "normal" => "normal",
            "internal" => "internal",
            "confidential" => "confidential",
            _ => fallback,
        };
    }

    private static string CleanText(string? value, string fallback, int maxLength)
    {
        var text = string.Join(' ', (value ?? string.Empty).Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(text))
        {
            text = fallback;
        }

        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";
    }

    private static string[] CleanList(string[]? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var date) ? date : null;
    }

    private sealed record DocumentUnderstandingJson(
        [property: JsonPropertyName("language")] string? Language,
        [property: JsonPropertyName("documentType")] string? DocumentType,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("keyTopics")] string[]? KeyTopics,
        [property: JsonPropertyName("entities")] string[]? Entities,
        [property: JsonPropertyName("effectiveDate")] string? EffectiveDate,
        [property: JsonPropertyName("sensitivity")] string? Sensitivity,
        [property: JsonPropertyName("qualityWarnings")] string[]? QualityWarnings);
}

public sealed record DocumentUnderstandingResult(
    string Language,
    string DocumentType,
    string Summary,
    IReadOnlyList<string> KeyTopics,
    IReadOnlyList<string> Entities,
    DateTimeOffset? EffectiveDate,
    string Sensitivity,
    IReadOnlyList<string> QualityWarnings);

internal static partial class DocumentUnderstandingHeuristics
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were", "has", "have",
        "mot", "cac", "cua", "cho", "voi", "khi", "thi", "la", "va", "de", "duoc", "trong", "ngoai",
        "neu", "sau", "truoc", "phai", "can", "nen", "nguoi", "dung", "noi", "bo", "quy", "trinh",
    };

    public static DocumentUnderstandingResult Analyze(
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        IReadOnlyList<string>? extraWarnings = null)
    {
        var searchable = $"{title} {normalizedText}";
        var language = LooksVietnamese(searchable) ? "vi" : "en";
        var topics = ExtractTopics(searchable);
        var warnings = new List<string>();
        if (normalizedText.Length < 200)
        {
            warnings.Add("document_text_short");
        }

        if (sections.Count == 0)
        {
            warnings.Add("no_section_outline_detected");
        }

        if (LooksMojibake(searchable))
        {
            warnings.Add("possible_encoding_issue");
        }

        if (extraWarnings is not null)
        {
            warnings.AddRange(extraWarnings);
        }

        return new DocumentUnderstandingResult(
            language,
            DetectDocumentType(searchable),
            BuildSummary(normalizedText),
            topics,
            ExtractEntities(searchable),
            ExtractEffectiveDate(searchable),
            DetectSensitivity(searchable),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string DetectDocumentType(string text)
    {
        if (ContainsAny(text, ["price", "pricing", "gia", "bao gia", "phi", "cost"]))
        {
            return "pricing";
        }

        if (ContainsAny(text, ["policy", "chinh sach", "quy dinh", "compliance"]))
        {
            return "policy";
        }

        if (ContainsAny(text, ["procedure", "process", "workflow", "step", "buoc", "quy trinh", "kiem tra", "duyet"]))
        {
            return "procedure";
        }

        if (ContainsAny(text, ["api", "token", "server", "database", "error", "log", "technical"]))
        {
            return "technical";
        }

        if (ContainsAny(text, ["contract", "agreement", "hop dong"]))
        {
            return "contract";
        }

        if (ContainsAny(text, ["faq", "q:", "a:", "hoi dap"]))
        {
            return "faq";
        }

        return "unknown";
    }

    private static string DetectSensitivity(string text)
    {
        if (ContainsAny(text, ["confidential", "mat", "bao mat", "secret", "restricted"]))
        {
            return "confidential";
        }

        if (ContainsAny(text, ["internal", "noi bo", "employee", "nhan vien"]))
        {
            return "internal";
        }

        return "normal";
    }

    private static string BuildSummary(string text)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 900 ? normalized : normalized[..900].TrimEnd() + "...";
    }

    private static string[] ExtractTopics(string text)
    {
        return TokenRegex().Matches(RemoveVietnameseMarks(text).ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length >= 4 && !StopWords.Contains(token))
            .GroupBy(token => token)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(12)
            .Select(group => group.Key)
            .ToArray();
    }

    private static string[] ExtractEntities(string text)
    {
        return EntityRegex().Matches(text)
            .Select(match => match.Value.Trim())
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static DateTimeOffset? ExtractEffectiveDate(string text)
    {
        var match = DateRegex().Matches(text).FirstOrDefault();
        return match?.Success == true && DateTimeOffset.TryParse(match.Value, out var date) ? date : null;
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksVietnamese(string text)
    {
        const string VietnameseCharacters = "\u0103\u00e2\u0111\u00ea\u00f4\u01a1\u01b0\u00e1\u00e0\u1ea3\u00e3\u1ea1\u1ea5\u1ea7\u1ea9\u1eab\u1ead\u1eaf\u1eb1\u1eb3\u1eb5\u1eb7\u00e9\u00e8\u1ebb\u1ebd\u1eb9\u1ebf\u1ec1\u1ec3\u1ec5\u1ec7\u00ed\u00ec\u1ec9\u0129\u1ecb\u00f3\u00f2\u1ecf\u00f5\u1ecd\u1ed1\u1ed3\u1ed5\u1ed7\u1ed9\u1edb\u1edd\u1edf\u1ee1\u1ee3\u00fa\u00f9\u1ee7\u0169\u1ee5\u1ee9\u1eeb\u1eed\u1eef\u1ef1\u00fd\u1ef3\u1ef7\u1ef9\u1ef5";
        return text.Any(character => VietnameseCharacters.Contains(char.ToLowerInvariant(character)))
            || ContainsAny(RemoveVietnameseMarks(text), ["quy trinh", "thanh toan", "noi bo", "kiem tra", "hoa don", "duyet"]);
    }

    private static bool LooksMojibake(string text)
    {
        return text.Contains("Ã", StringComparison.Ordinal) || text.Contains("Â", StringComparison.Ordinal);
    }

    private static string RemoveVietnameseMarks(string text)
    {
        var normalized = text
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(System.Text.NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\b[A-Z][A-Za-z0-9]*(?:\s+[A-Z][A-Za-z0-9]*){0,3}\b", RegexOptions.Compiled)]
    private static partial Regex EntityRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}/\d{1,2}/\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex DateRegex();
}
