using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<string> SaveDocumentVersionAsync(Guid documentId, Guid versionId, IFormFile file, CancellationToken cancellationToken = default);

    bool TryResolveStoredPath(string storedPath, out string resolvedPath);
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

    public bool TryResolveStoredPath(string storedPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        var rootPath = EnsureTrailingSeparator(Path.GetFullPath(options.Value.RootPath));
        var candidatePath = Path.GetFullPath(storedPath);
        if (!candidatePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedPath = candidatePath;
        return true;
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

        if (safeName.Length > 120)
        {
            safeName = safeName[..120];
        }

        return $"{Guid.NewGuid():N}-{safeName}{extension}";
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
