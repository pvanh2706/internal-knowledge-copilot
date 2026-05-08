namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class AppStorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = "./storage";

    public long MaxUploadBytes { get; init; } = 20 * 1024 * 1024;

    public string[] AllowedExtensions { get; init; } = [".pdf", ".docx", ".md", ".markdown", ".txt"];
}
