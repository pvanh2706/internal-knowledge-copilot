using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Documents;

public sealed class FileUploadValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_ForAllowedTxtFile()
    {
        var validator = CreateValidator();
        var file = CreateFile("guide.txt", 100);

        var result = validator.Validate(file);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForUnsupportedExtension()
    {
        var validator = CreateValidator();
        var file = CreateFile("malware.exe", 100);

        var result = validator.Validate(file);

        Assert.False(result.IsValid);
        Assert.Equal("file_type_not_allowed", result.ErrorCode);
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForLargeFile()
    {
        var validator = CreateValidator(maxUploadBytes: 50);
        var file = CreateFile("guide.txt", 100);

        var result = validator.Validate(file);

        Assert.False(result.IsValid);
        Assert.Equal("file_too_large", result.ErrorCode);
    }

    private static FileUploadValidator CreateValidator(long maxUploadBytes = 20 * 1024 * 1024)
    {
        return new FileUploadValidator(Options.Create(new AppStorageOptions
        {
            RootPath = "./storage",
            MaxUploadBytes = maxUploadBytes,
            AllowedExtensions = [".pdf", ".docx", ".md", ".markdown", ".txt"],
        }));
    }

    private static IFormFile CreateFile(string fileName, int length)
    {
        var content = new byte[length];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, length, "file", fileName);
    }
}
