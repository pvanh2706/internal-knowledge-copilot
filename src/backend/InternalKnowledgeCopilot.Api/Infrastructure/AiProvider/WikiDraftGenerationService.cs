namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IWikiDraftGenerationService
{
    WikiDraftContent Generate(string title, string sourceText);
}

public sealed class MockWikiDraftGenerationService : IWikiDraftGenerationService
{
    public WikiDraftContent Generate(string title, string sourceText)
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

        return new WikiDraftContent(content, language);
    }

    private static bool LooksVietnamese(string text)
    {
        return text.Any(character => "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ".Contains(char.ToLowerInvariant(character)));
    }
}

public sealed record WikiDraftContent(string Content, string Language);
