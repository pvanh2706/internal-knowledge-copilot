using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IWikiDraftGenerationService
{
    Task<WikiDraftContent> GenerateAsync(string title, string sourceText, CancellationToken cancellationToken = default);
}

public sealed class MockWikiDraftGenerationService : IWikiDraftGenerationService
{
    public Task<WikiDraftContent> GenerateAsync(string title, string sourceText, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeText(sourceText);
        var summary = Trim(normalized, 700);
        var language = LooksVietnamese(normalized) ? "vi" : "en";
        var content = $"""
            # {title}

            ## Purpose

            {summary}

            ## Scope

            Applies to the process described in the source document.

            ## Audience

            - Internal users who follow or review this process.

            ## Main content

            {BuildMainContent(normalized)}

            ## Procedure

            {BuildProcedure(normalized)}

            ## Risks and notes

            - Review the source document before publishing this draft.

            ## FAQ

            - Q: What source was used?
              A: {title}

            ## Missing information

            - No explicit owner or effective date was found in the source.

            ## Sources

            - Created from document: {title}
            """;

        return Task.FromResult(new WikiDraftContent(
            content,
            language,
            ["No explicit owner or effective date was found in the source."]));
    }

    private static string NormalizeText(string text)
    {
        return string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildMainContent(string normalized)
    {
        var sentences = SplitSentences(normalized).Take(4).ToArray();
        return sentences.Length == 0
            ? "The source document does not contain enough detail for this section."
            : string.Join(Environment.NewLine, sentences.Select(sentence => $"- {sentence}"));
    }

    private static string BuildProcedure(string normalized)
    {
        var procedureSentences = SplitSentences(normalized)
            .Where(sentence => ContainsAny(sentence, ["step", "check", "review", "approve", "retry", "submit", "create", "kiem tra", "duyet"]))
            .Take(5)
            .ToArray();

        return procedureSentences.Length == 0
            ? "- The source document does not define step-by-step procedure details."
            : string.Join(Environment.NewLine, procedureSentences.Select((sentence, index) => $"{index + 1}. {sentence}"));
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        return text
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0);
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";
    }

    private static bool LooksVietnamese(string text)
    {
        const string VietnameseCharacters = "\u0103\u00e2\u0111\u00ea\u00f4\u01a1\u01b0\u00e1\u00e0\u1ea3\u00e3\u1ea1\u1ea5\u1ea7\u1ea9\u1eab\u1ead\u1eaf\u1eb1\u1eb3\u1eb5\u1eb7\u00e9\u00e8\u1ebb\u1ebd\u1eb9\u1ebf\u1ec1\u1ec3\u1ec5\u1ec7\u00ed\u00ec\u1ec9\u0129\u1ecb\u00f3\u00f2\u1ecf\u00f5\u1ecd\u1ed1\u1ed3\u1ed5\u1ed7\u1ed9\u1edb\u1edd\u1edf\u1ee1\u1ee3\u00fa\u00f9\u1ee7\u0169\u1ee5\u1ee9\u1eeb\u1eed\u1eef\u1ef1\u00fd\u1ef3\u1ef7\u1ef9\u1ef5";
        return text.Any(character => VietnameseCharacters.Contains(char.ToLowerInvariant(character)));
    }
}

public sealed class OpenAiCompatibleWikiDraftGenerationService(OpenAiCompatibleClient client) : IWikiDraftGenerationService
{
    private const int MaxSourceCharacters = 14000;

    public async Task<WikiDraftContent> GenerateAsync(string title, string sourceText, CancellationToken cancellationToken = default)
    {
        var normalized = string.Join(' ', sourceText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        var language = LooksVietnamese(normalized) ? "vi" : "en";
        var trimmedSource = normalized.Length <= MaxSourceCharacters ? normalized : normalized[..MaxSourceCharacters].TrimEnd() + "...";

        var systemPrompt = """
            You create reviewer-ready internal wiki drafts from approved source documents.
            Write in Vietnamese unless the source is clearly not Vietnamese.
            Use only the source document content.
            Do not invent owners, dates, policies, prices, SLAs, or approval rules.
            Return JSON only with this schema:
            {
              "title": "string",
              "language": "vi|en",
              "purpose": "string",
              "scope": "string",
              "audience": ["string"],
              "mainContent": [{ "heading": "string", "content": "string" }],
              "procedure": ["string"],
              "risksAndNotes": ["string"],
              "faq": [{ "question": "string", "answer": "string" }],
              "missingInformation": ["string"]
            }
            Put missing details in missingInformation instead of guessing.
            """;

        var userPrompt = $"""
            Source document title: {title}

            Source document text:
            {trimmedSource}

            Create a structured wiki draft JSON. Make it reviewer-ready and concise.
            """;

        var rawDraft = await client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        if (TryParseStructuredDraft(rawDraft, title, language, out var parsedDraft))
        {
            return parsedDraft;
        }

        var repairPrompt = $"""
            The previous output was not valid JSON for the required wiki draft schema.

            Previous output:
            {rawDraft}

            Re-read the source and return valid JSON only.

            Original request:
            {userPrompt}
            """;

        var repairedDraft = await client.CompleteAsync(systemPrompt, repairPrompt, cancellationToken);
        return TryParseStructuredDraft(repairedDraft, title, language, out var repaired)
            ? repaired
            : BuildFallbackDraft(title, rawDraft, language);
    }

    private static bool TryParseStructuredDraft(string rawDraft, string fallbackTitle, string fallbackLanguage, out WikiDraftContent draft)
    {
        draft = null!;
        var json = ExtractJsonObject(rawDraft);
        if (json is null)
        {
            return false;
        }

        StructuredWikiDraftJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<StructuredWikiDraftJson>(json, new JsonSerializerOptions
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

        var title = string.IsNullOrWhiteSpace(parsed.Title) ? fallbackTitle : parsed.Title.Trim();
        var language = string.IsNullOrWhiteSpace(parsed.Language) ? fallbackLanguage : parsed.Language.Trim();
        var missing = CleanList(parsed.MissingInformation);
        var content = ToMarkdown(title, parsed, missing);
        draft = new WikiDraftContent(content, language, missing);
        return true;
    }

    private static WikiDraftContent BuildFallbackDraft(string title, string rawDraft, string language)
    {
        var content = $"""
            # {title}

            ## Purpose

            The AI provider did not return a valid structured wiki draft.

            ## Main content

            {rawDraft.Trim()}

            ## Missing information

            - AI provider output was not valid JSON.
            """;

        return new WikiDraftContent(content, language, ["AI provider output was not valid JSON."]);
    }

    private static string ToMarkdown(string title, StructuredWikiDraftJson parsed, IReadOnlyList<string> missing)
    {
        var audience = CleanList(parsed.Audience);
        var procedure = CleanList(parsed.Procedure);
        var risks = CleanList(parsed.RisksAndNotes);
        var mainContent = (parsed.MainContent ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Heading) || !string.IsNullOrWhiteSpace(item.Content))
            .ToArray();
        var faq = (parsed.Faq ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Question) || !string.IsNullOrWhiteSpace(item.Answer))
            .ToArray();

        return $"""
            # {title}

            ## Purpose

            {ValueOrMissing(parsed.Purpose)}

            ## Scope

            {ValueOrMissing(parsed.Scope)}

            ## Audience

            {ToBullets(audience)}

            ## Main content

            {ToMainContent(mainContent)}

            ## Procedure

            {ToNumberedList(procedure)}

            ## Risks and notes

            {ToBullets(risks)}

            ## FAQ

            {ToFaq(faq)}

            ## Missing information

            {ToBullets(missing)}
            """;
    }

    private static string ValueOrMissing(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "No information in source document." : value.Trim();
    }

    private static string ToBullets(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "- No information in source document."
            : string.Join(Environment.NewLine, values.Select(value => $"- {value}"));
    }

    private static string ToNumberedList(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "1. No step-by-step procedure found in source document."
            : string.Join(Environment.NewLine, values.Select((value, index) => $"{index + 1}. {value}"));
    }

    private static string ToMainContent(IReadOnlyList<StructuredWikiMainContentJson> values)
    {
        if (values.Count == 0)
        {
            return "No information in source document.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            values.Select(value => $"### {ValueOrMissing(value.Heading)}{Environment.NewLine}{ValueOrMissing(value.Content)}"));
    }

    private static string ToFaq(IReadOnlyList<StructuredWikiFaqJson> values)
    {
        if (values.Count == 0)
        {
            return "- No FAQ could be derived from the source document.";
        }

        return string.Join(
            Environment.NewLine,
            values.Select(value => $"- Q: {ValueOrMissing(value.Question)}{Environment.NewLine}  A: {ValueOrMissing(value.Answer)}"));
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
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

    private static bool LooksVietnamese(string text)
    {
        const string VietnameseCharacters = "\u0103\u00e2\u0111\u00ea\u00f4\u01a1\u01b0\u00e1\u00e0\u1ea3\u00e3\u1ea1\u1ea5\u1ea7\u1ea9\u1eab\u1ead\u1eaf\u1eb1\u1eb3\u1eb5\u1eb7\u00e9\u00e8\u1ebb\u1ebd\u1eb9\u1ebf\u1ec1\u1ec3\u1ec5\u1ec7\u00ed\u00ec\u1ec9\u0129\u1ecb\u00f3\u00f2\u1ecf\u00f5\u1ecd\u1ed1\u1ed3\u1ed5\u1ed7\u1ed9\u1edb\u1edd\u1edf\u1ee1\u1ee3\u00fa\u00f9\u1ee7\u0169\u1ee5\u1ee9\u1eeb\u1eed\u1eef\u1ef1\u00fd\u1ef3\u1ef7\u1ef9\u1ef5";
        return text.Any(character => VietnameseCharacters.Contains(char.ToLowerInvariant(character)));
    }
}

public sealed record WikiDraftContent(string Content, string Language, IReadOnlyList<string> MissingInformation);

internal sealed record StructuredWikiDraftJson(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("purpose")] string? Purpose,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("audience")] string[]? Audience,
    [property: JsonPropertyName("mainContent")] StructuredWikiMainContentJson[]? MainContent,
    [property: JsonPropertyName("procedure")] string[]? Procedure,
    [property: JsonPropertyName("risksAndNotes")] string[]? RisksAndNotes,
    [property: JsonPropertyName("faq")] StructuredWikiFaqJson[]? Faq,
    [property: JsonPropertyName("missingInformation")] string[]? MissingInformation);

internal sealed record StructuredWikiMainContentJson(
    [property: JsonPropertyName("heading")] string? Heading,
    [property: JsonPropertyName("content")] string? Content);

internal sealed record StructuredWikiFaqJson(
    [property: JsonPropertyName("question")] string? Question,
    [property: JsonPropertyName("answer")] string? Answer);
