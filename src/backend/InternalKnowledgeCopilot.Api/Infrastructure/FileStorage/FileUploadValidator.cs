using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;

public interface IFileUploadValidator
{
    FileValidationResult Validate(IFormFile? file);
}

public sealed class FileUploadValidator(IOptions<AppStorageOptions> options) : IFileUploadValidator
{
    public FileValidationResult Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return FileValidationResult.Invalid("file_required", "File là bắt buộc.");
        }

        if (file.Length > options.Value.MaxUploadBytes)
        {
            return FileValidationResult.Invalid("file_too_large", "File vượt quá giới hạn 20MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = options.Value.AllowedExtensions.Select(item => item.ToLowerInvariant()).ToHashSet();
        if (!allowedExtensions.Contains(extension))
        {
            return FileValidationResult.Invalid("file_type_not_allowed", "Chỉ hỗ trợ PDF, DOCX, Markdown và TXT.");
        }

        return FileValidationResult.Valid();
    }
}
