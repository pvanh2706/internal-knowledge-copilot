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
                "Mình chưa tìm thấy thông tin đủ rõ trong phạm vi bạn chọn. Bạn có thể hỏi cụ thể hơn về tài liệu, folder hoặc quy trình cần tra cứu không?",
                true));
        }

        var excerpts = chunks
            .Take(3)
            .Select((chunk, index) => $"{index + 1}. {TrimExcerpt(chunk.Text, MaxAnswerExcerptLength / Math.Min(3, chunks.Count))}")
            .ToList();

        var answer = "Dựa trên các nguồn tìm được, mình tóm tắt như sau:\n" + string.Join("\n", excerpts);
        return Task.FromResult(new AiAnswerDraft(answer, false));
    }

    private static bool HasKeywordOverlap(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks)
    {
        var questionTokens = Tokenize(question).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (questionTokens.Count == 0)
        {
            return false;
        }

        return chunks.Any(chunk => Tokenize(chunk.Text).Any(questionTokens.Contains));
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
                "Mình chưa tìm thấy nguồn phù hợp trong phạm vi bạn được phép xem. Bạn có thể hỏi cụ thể hơn hoặc chọn folder/tài liệu khác.",
                true);
        }

        var systemPrompt = """
            You are Internal Knowledge Copilot for a Vietnamese internal knowledge base.
            Answer in Vietnamese.
            Use only the provided sources.
            Do not invent facts, policies, prices, dates, or procedures.
            If the sources are insufficient or ambiguous, say what is missing and ask a concise clarifying question.
            Reference source labels like [S1], [S2] when making claims.
            Never mention sources that are not included in the prompt.
            """;

        var sourceBlocks = chunks
            .Select((chunk, index) => $"""
                [S{index + 1}]
                Type: {chunk.SourceType}
                Title: {chunk.Title}
                Folder: {chunk.FolderPath}
                Text:
                {Trim(chunk.Text, MaxChunkCharacters)}
                """)
            .ToList();

        var userPrompt = $"""
            Question:
            {question}

            Sources:
            {string.Join("\n\n", sourceBlocks)}

            Return a direct, grounded answer. If the answer cannot be determined from the sources, state that clearly.
            """;

        var answer = await client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        var needsClarification = answer.Contains("chưa đủ", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("không đủ", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("cần thêm", StringComparison.OrdinalIgnoreCase);

        return new AiAnswerDraft(answer, needsClarification);
    }

    private static string Trim(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd() + "...";
    }
}

public sealed record AiAnswerDraft(string Answer, bool NeedsClarification);
