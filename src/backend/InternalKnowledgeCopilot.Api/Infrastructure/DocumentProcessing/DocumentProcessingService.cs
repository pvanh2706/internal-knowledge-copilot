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
    IDocumentTextNormalizer textNormalizer,
    ISectionDetector sectionDetector,
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

        var storageDirectory = Path.GetDirectoryName(version.StoredFilePath)!;
        var extractedTextPath = Path.Combine(storageDirectory, "extracted.txt");
        await File.WriteAllTextAsync(extractedTextPath, extractedText, cancellationToken);
        var normalized = textNormalizer.Normalize(extractedText);
        var normalizedTextPath = Path.Combine(storageDirectory, "normalized.txt");
        await File.WriteAllTextAsync(normalizedTextPath, normalized.Text, cancellationToken);
        var normalizedBytes = System.Text.Encoding.UTF8.GetBytes(normalized.Text);
        var textHash = Convert.ToHexString(SHA256.HashData(normalizedBytes)).ToLowerInvariant();

        var sections = sectionDetector.Detect(normalized.Text);
        var chunks = chunker.Chunk(normalized.Text, sections);
        var vectorChunks = new List<KnowledgeChunkRecord>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var chunkId = $"{version.Id:N}-{chunk.Index}";
            vectorChunks.Add(new KnowledgeChunkRecord(
                chunkId,
                await embeddingService.CreateEmbeddingAsync(chunk.Text, cancellationToken),
                chunk.Text,
                new Dictionary<string, object>
                {
                    ["chunk_id"] = chunkId,
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
                    ["section_title"] = chunk.SectionTitle ?? string.Empty,
                    ["section_index"] = chunk.SectionIndex ?? -1,
                    ["char_start"] = chunk.StartOffset ?? 0,
                    ["char_end"] = chunk.EndOffset ?? 0,
                    ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                }));
        }

        await vectorStore.UpsertChunksAsync(vectorChunks, cancellationToken);

        version.ExtractedTextPath = extractedTextPath;
        version.NormalizedTextPath = normalizedTextPath;
        version.SectionCount = sections.Count;
        version.ProcessingWarningsJson = normalized.WarningsJson;
        version.DocumentSummary = BuildSummary(sections, normalized.Text);
        version.TextHash = textHash;
        version.Status = DocumentVersionStatus.Indexed;
        version.IndexedAt = DateTimeOffset.UtcNow;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSummary(IReadOnlyList<DocumentSection> sections, string normalizedText)
    {
        var candidate = sections.Count > 0
            ? sections[0].Text
            : normalizedText;

        var summary = string.Join(' ', candidate.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return summary.Length <= 600 ? summary : summary[..600].TrimEnd() + "...";
    }
}
