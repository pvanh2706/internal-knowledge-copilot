using System.Security.Cryptography;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface IDocumentProcessingService
{
    Task ProcessDocumentVersionAsync(Guid documentVersionId, CancellationToken cancellationToken = default);
}

public sealed class DocumentProcessingService(
    AppDbContext dbContext,
    IDocumentTextExtractor textExtractor,
    ITextChunker chunker,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore) : IDocumentProcessingService
{
    public async Task ProcessDocumentVersionAsync(Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.DocumentVersions
            .Include(item => item.Document)
                .ThenInclude(document => document!.Folder)
            .FirstOrDefaultAsync(item => item.Id == documentVersionId, cancellationToken);

        if (version is null || version.Document is null)
        {
            throw new InvalidOperationException("Document version not found.");
        }

        version.Status = DocumentVersionStatus.Processing;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var extractedText = await textExtractor.ExtractAsync(version.StoredFilePath, version.FileExtension, cancellationToken);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException("Document has no extractable text.");
        }

        var extractedTextPath = Path.Combine(Path.GetDirectoryName(version.StoredFilePath)!, "extracted.txt");
        await File.WriteAllTextAsync(extractedTextPath, extractedText, cancellationToken);
        var textHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(extractedText))).ToLowerInvariant();

        var chunks = chunker.Chunk(extractedText);
        var vectorChunks = chunks.Select(chunk => new KnowledgeChunkRecord(
            $"{version.Id:N}-{chunk.Index}",
            embeddingService.CreateEmbedding(chunk.Text),
            chunk.Text,
            new Dictionary<string, object>
            {
                ["chunk_id"] = $"{version.Id:N}-{chunk.Index}",
                ["source_type"] = "document",
                ["source_id"] = version.Id.ToString(),
                ["document_id"] = version.DocumentId.ToString(),
                ["document_version_id"] = version.Id.ToString(),
                ["folder_id"] = version.Document.FolderId.ToString(),
                ["title"] = version.Document.Title,
                ["folder_path"] = version.Document.Folder?.Path ?? string.Empty,
                ["version_number"] = version.VersionNumber,
                ["status"] = "approved",
                ["visibility_scope"] = "folder",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            })).ToList();

        await vectorStore.UpsertChunksAsync(vectorChunks, cancellationToken);

        version.ExtractedTextPath = extractedTextPath;
        version.TextHash = textHash;
        version.Status = DocumentVersionStatus.Indexed;
        version.IndexedAt = DateTimeOffset.UtcNow;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
