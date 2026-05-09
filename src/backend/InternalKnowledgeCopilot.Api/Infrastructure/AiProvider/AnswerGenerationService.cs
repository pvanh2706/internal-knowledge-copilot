using InternalKnowledgeCopilot.Api.Modules.Ai;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IAnswerGenerationService
{
    AiAnswerDraft Generate(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks);
}

public sealed class MockAnswerGenerationService : IAnswerGenerationService
{
    private const int MaxAnswerExcerptLength = 900;

    public AiAnswerDraft Generate(string question, IReadOnlyList<RetrievedKnowledgeChunk> chunks)
    {
        if (chunks.Count == 0 || !HasKeywordOverlap(question, chunks))
        {
            return new AiAnswerDraft(
                "Mình chưa tìm thấy thông tin đủ rõ trong phạm vi bạn chọn. Bạn có thể hỏi cụ thể hơn về tài liệu, folder hoặc quy trình cần tra cứu không?",
                true);
        }

        var excerpts = chunks
            .Take(3)
            .Select((chunk, index) => $"{index + 1}. {TrimExcerpt(chunk.Text, MaxAnswerExcerptLength / Math.Min(3, chunks.Count))}")
            .ToList();

        var answer = "Dựa trên các nguồn tìm được, mình tóm tắt như sau:\n" + string.Join("\n", excerpts);
        return new AiAnswerDraft(answer, false);
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
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd() + "...";
    }
}

public sealed record AiAnswerDraft(string Answer, bool NeedsClarification);
