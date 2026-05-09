using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<string> SaveDocumentVersionAsync(Guid documentId, Guid versionId, IFormFile file, CancellationToken cancellationToken = default);
}

public sealed class FileStorageService(IOptions<AppStorageOptions> options) : IFileStorageService
{
    public async Task<string> SaveDocumentVersionAsync(Guid documentId, Guid versionId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var rootPath = Path.GetFullPath(options.Value.RootPath);
        var versionDirectory = Path.Combine(rootPath, "documents", documentId.ToString(), versionId.ToString());
        Directory.CreateDirectory(versionDirectory);

        var safeFileName = BuildSafeFileName(file.FileName);
        var storedPath = Path.Combine(versionDirectory, safeFileName);
        await using var output = File.Create(storedPath);
        await file.CopyToAsync(output, cancellationToken);

        return storedPath;
    }

    private static string BuildSafeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(rawName.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "document";
        }

        return $"{Guid.NewGuid():N}-{safeName}{extension}";
    }
}
