using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Documents;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "ikc-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveDocumentVersionAsync_StoresTraversalFileNameInsideStorageRoot()
    {
        var service = CreateService();
        var file = CreateFile(@"..\..\secret.txt", "safe content");

        var storedPath = await service.SaveDocumentVersionAsync(Guid.NewGuid(), Guid.NewGuid(), file);

        Assert.True(service.TryResolveStoredPath(storedPath, out var resolvedPath));
        Assert.StartsWith(Path.GetFullPath(rootPath), resolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(resolvedPath));
        Assert.DoesNotContain("..", Path.GetFileName(resolvedPath), StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveStoredPath_ReturnsFalse_ForPathOutsideStorageRoot()
    {
        var service = CreateService();
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside.txt");

        var allowed = service.TryResolveStoredPath(outsidePath, out _);

        Assert.False(allowed);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private FileStorageService CreateService()
    {
        return new FileStorageService(Options.Create(new AppStorageOptions
        {
            RootPath = rootPath,
            MaxUploadBytes = 20 * 1024 * 1024,
            AllowedExtensions = [".pdf", ".docx", ".md", ".markdown", ".txt"],
        }));
    }

    private static IFormFile CreateFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }
}
