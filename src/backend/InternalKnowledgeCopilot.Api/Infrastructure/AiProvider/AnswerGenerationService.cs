using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Modules.Ai;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IAnswerGenerationService
{
    Task<AiAnswerDraft> GenerateAsync(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks, CancellationToken cancellationToken = default);
}

public sealed class MockAnswerGenerationService : IAnswerGenerationService
{
    private const int MaxAnswerExcerptLength = 900;

    public Task<AiAnswerDraft> GenerateAsync(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0 || !HasKeywordOverlap(question, chunks))
        {
            return Task.FromResult(new AiAnswerDraft(
                "Minh chua tim thay thong tin du ro trong pham vi ban chon. Ban co the hoi cu the hon ve tai lieu, folder hoac quy trinh can tra cuu khong?",
                true,
                "low",
                ["Khong co nguon phu hop trong context da truy xuat."],
                [],
                ["Hoi cu the hon hoac chon folder/tai lieu lien quan."],
                []));
        }

        var excerpts = chunks
            .Take(3)
            .Select((chunk, index) => $"{index + 1}. {TrimExcerpt(chunk.Text, MaxAnswerExcerptLength / Math.Min(3, chunks.Count))}")
            .ToList();

        var answer = "Dua tren cac nguon tim duoc, minh tom tat nhu sau:\n" + string.Join("\n", excerpts);
        return Task.FromResult(new AiAnswerDraft(
            answer,
            false,
            chunks.Count >= 2 ? "medium" : "low",
            [],
            [],
            [],
            chunks.Take(3).Select(chunk => chunk.SourceId).ToArray()));
    }

    private static bool HasKeywordOverlap(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks)
    {
        var questionTokens = Tokenize(question).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return questionTokens.Count > 0 && chunks.Any(chunk => Tokenize(chunk.Text).Any(questionTokens.Contains));
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length >= 3);
    }

    private static string TrimExcerpt(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd() + "...";
    }
}

public sealed class OpenAiCompatibleAnswerGenerationService(OpenAiCompatibleClient client) : IAnswerGenerationService
{
    private const int MaxChunkCharacters = 1600;

    public async Task<AiAnswerDraft> GenerateAsync(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return new AiAnswerDraft(
                "Minh chua tim thay nguon phu hop trong pham vi ban duoc phep xem. Ban co the hoi cu the hon hoac chon folder/tai lieu khac.",
                true,
                "low",
                ["Khong co nguon phu hop trong context da truy xuat."],
                [],
                ["Hoi cu the hon hoac chon folder/tai lieu khac."],
                []);
        }

        var systemPrompt = """
            You are Internal Knowledge Copilot for a Vietnamese internal knowledge base.
            Answer in Vietnamese.
            Use only the provided sources.
            Do not invent facts, policies, prices, dates, or procedures.
            If the sources are insufficient or ambiguous, say what is missing and ask a concise clarifying question.
            Never mention sources that are not included in the prompt.
            Return JSON only, with this schema:
            {
              "answer": "string",
              "confidence": "high|medium|low",
              "needsClarification": true,
              "clarifyingQuestion": "string|null",
              "citations": [{ "sourceId": "S1" }],
              "missingInformation": ["string"],
              "conflicts": ["string"],
              "suggestedFollowUps": ["string"]
            }
            Only cite sourceId values provided in the prompt.
            """;

        var sourceBlocks = chunks
            .Select((chunk, index) => $"""
                [S{index + 1}]
                SourceId: S{index + 1}
                Type: {chunk.SourceType}
                Title: {chunk.Title}
                Folder: {chunk.FolderPath}
                Section: {chunk.SectionTitle ?? "-"}
                Text:
                {Trim(chunk.Text, MaxChunkCharacters)}
                """)
            .ToList();

        var userPrompt = $"""
            Question:
            {question}

            Sources:
            {string.Join("\n\n", sourceBlocks)}

            Return a grounded JSON answer. If the answer cannot be determined from the sources, set confidence to "low",
            needsClarification to true, fill missingInformation, and do not cite irrelevant sources.
            """;

        var rawAnswer = await client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        if (TryParseGroundedAnswer(rawAnswer, chunks, out var draft))
        {
            return draft;
        }

        var repairPrompt = $"""
            The previous model output was not valid JSON for the required schema.

            Previous output:
            {rawAnswer}

            Re-read the original question and sources, then return valid JSON only using the required schema.

            Original question and sources:
            {userPrompt}
            """;

        var repairedAnswer = await client.CompleteAsync(systemPrompt, repairPrompt, cancellationToken);
        return TryParseGroundedAnswer(repairedAnswer, chunks, out var repairedDraft)
            ? repairedDraft
            : BuildFallbackDraft(rawAnswer);
    }

    private static bool TryParseGroundedAnswer(string rawAnswer, IReadOnlyList<RetrievedKnowledgeChunk> chunks, out AiAnswerDraft draft)
    {
        draft = null!;
        var json = ExtractJsonObject(rawAnswer);
        if (json is null)
        {
            return false;
        }

        GroundedAnswerJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GroundedAnswerJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Answer))
        {
            return false;
        }

        var validSourceIds = chunks
            .Select((chunk, index) => new { Label = $"S{index + 1}", chunk.SourceId })
            .ToDictionary(item => item.Label, item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var citedSourceIds = (parsed.Citations ?? [])
            .Select(citation => citation.SourceId?.Trim())
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Select(sourceId => validSourceIds.TryGetValue(sourceId!, out var realSourceId) ? realSourceId : null)
            .Where(sourceId => sourceId is not null)
            .Select(sourceId => sourceId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var confidence = NormalizeConfidence(parsed.Confidence);
        if (citedSourceIds.Length == 0 && !parsed.NeedsClarification)
        {
            confidence = "low";
        }

        draft = new AiAnswerDraft(
            parsed.Answer.Trim(),
            parsed.NeedsClarification || (confidence == "low" && (parsed.MissingInformation?.Length ?? 0) > 0),
            confidence,
            CleanList(parsed.MissingInformation),
            CleanList(parsed.Conflicts),
            CleanList(parsed.SuggestedFollowUps),
            citedSourceIds);
        return true;
    }

    private static AiAnswerDraft BuildFallbackDraft(string rawAnswer)
    {
        return new AiAnswerDraft(
            string.IsNullOrWhiteSpace(rawAnswer)
                ? "Minh chua the tao cau tra loi co cau truc tu provider AI."
                : rawAnswer.Trim(),
            true,
            "low",
            ["Provider AI khong tra ve JSON dung schema."],
            [],
            ["Thu hoi lai hoac kiem tra cau hinh AI provider."],
            []);
    }

    private static string? ExtractJsonObject(string rawAnswer)
    {
        var start = rawAnswer.IndexOf('{');
        var end = rawAnswer.LastIndexOf('}');
        return start >= 0 && end > start ? rawAnswer[start..(end + 1)] : null;
    }

    private static string NormalizeConfidence(string? confidence)
    {
        return confidence?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "medium" => "medium",
            _ => "low",
        };
    }

    private static string[] CleanList(string[]? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static string Trim(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd() + "...";
    }

    private sealed record GroundedAnswerJson(
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("confidence")] string? Confidence,
        [property: JsonPropertyName("needsClarification")] bool NeedsClarification,
        [property: JsonPropertyName("clarifyingQuestion")] string? ClarifyingQuestion,
        [property: JsonPropertyName("citations")] GroundedAnswerCitationJson[]? Citations,
        [property: JsonPropertyName("missingInformation")] string[]? MissingInformation,
        [property: JsonPropertyName("conflicts")] string[]? Conflicts,
        [property: JsonPropertyName("suggestedFollowUps")] string[]? SuggestedFollowUps);

    private sealed record GroundedAnswerCitationJson([property: JsonPropertyName("sourceId")] string? SourceId);
}

public sealed record AiAnswerDraft(
    string Answer,
    bool NeedsClarification,
    string Confidence,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> SuggestedFollowUps,
    IReadOnlyList<string> CitedSourceIds);
