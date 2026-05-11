namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IWikiDraftGenerationService
{
    Task<WikiDraftContent> GenerateAsync(string title, string sourceText, CancellationToken cancellationToken = default);
}

public sealed class MockWikiDraftGenerationService : IWikiDraftGenerationService
{
    public Task<WikiDraftContent> GenerateAsync(string title, string sourceText, CancellationToken cancellationToken = default)
    {
        var normalized = string.Join(' ', sourceText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        var summary = normalized.Length <= 900 ? normalized : normalized[..900].TrimEnd() + "...";
        var language = LooksVietnamese(normalized) ? "vi" : "en";
        var content = $"""
            # {title}

            ## Mục đích

            {summary}

            ## Phạm vi áp dụng

            Chưa có thông tin trong tài liệu nguồn.

            ## Nội dung chính

            {summary}

            ## Quy trình / Các bước thực hiện

            Chưa có thông tin trong tài liệu nguồn.

            ## Lưu ý quan trọng

            Chưa có thông tin trong tài liệu nguồn.

            ## Câu hỏi thường gặp

            Chưa có thông tin trong tài liệu nguồn.

            ## Nguồn

            Tạo từ tài liệu: {title}
            """;

        return Task.FromResult(new WikiDraftContent(content, language));
    }

    private static bool LooksVietnamese(string text)
    {
        return text.Any(character => "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ".Contains(char.ToLowerInvariant(character)));
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
            If information is missing, write "Chưa có thông tin trong tài liệu nguồn." for that section.
            Return Markdown only.
            """;

        var userPrompt = $"""
            Source document title: {title}

            Source document text:
            {trimmedSource}

            Create a structured wiki draft with these sections:
            # {title}
            ## Mục đích
            ## Phạm vi áp dụng
            ## Đối tượng sử dụng
            ## Nội dung chính
            ## Quy trình / Các bước thực hiện
            ## Lưu ý quan trọng
            ## Câu hỏi thường gặp
            ## Nguồn
            """;

        var content = await client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        return new WikiDraftContent(content, language);
    }

    private static bool LooksVietnamese(string text)
    {
        return text.Any(character => "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ".Contains(char.ToLowerInvariant(character)));
    }
}

public sealed record WikiDraftContent(string Content, string Language);
