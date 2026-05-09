using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Wiki;

public interface IWikiService
{
    Task<WikiDraftDetailResponse> GenerateDraftAsync(Guid reviewerId, GenerateWikiDraftRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WikiDraftListItemResponse>> GetDraftsAsync(CancellationToken cancellationToken = default);

    Task<WikiDraftDetailResponse> GetDraftAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WikiPageResponse> PublishAsync(Guid draftId, Guid reviewerId, PublishWikiDraftRequest request, CancellationToken cancellationToken = default);

    Task<WikiDraftDetailResponse> RejectAsync(Guid draftId, Guid reviewerId, RejectWikiDraftRequest request, CancellationToken cancellationToken = default);
}

public sealed class WikiService(
    AppDbContext dbContext,
    IFolderPermissionService folderPermissionService,
    IWikiDraftGenerationService draftGenerationService,
    ITextChunker chunker,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IAuditLogService auditLogService) : IWikiService
{
    public async Task<WikiDraftDetailResponse> GenerateDraftAsync(Guid reviewerId, GenerateWikiDraftRequest request, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.DocumentVersions
            .Include(item => item.Document)
                .ThenInclude(document => document!.Folder)
            .FirstOrDefaultAsync(item => item.Id == request.DocumentVersionId && item.DocumentId == request.DocumentId, cancellationToken);

        if (version?.Document is null || version.Document.DeletedAt is not null)
        {
            throw new KeyNotFoundException("document_version_not_found");
        }

        if (version.Status != DocumentVersionStatus.Indexed || version.Document.CurrentVersionId != version.Id)
        {
            throw new InvalidOperationException("document_version_not_indexed");
        }

        if (!await folderPermissionService.CanViewFolderAsync(reviewerId, version.Document.FolderId, cancellationToken))
        {
            throw new UnauthorizedAccessException("folder_forbidden");
        }

        if (string.IsNullOrWhiteSpace(version.ExtractedTextPath) || !File.Exists(version.ExtractedTextPath))
        {
            throw new InvalidOperationException("extracted_text_not_found");
        }

        var sourceText = await File.ReadAllTextAsync(version.ExtractedTextPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new InvalidOperationException("extracted_text_empty");
        }

        var generated = draftGenerationService.Generate(version.Document.Title, sourceText);
        var now = DateTimeOffset.UtcNow;
        var draft = new WikiDraftEntity
        {
            Id = Guid.NewGuid(),
            SourceDocumentId = version.DocumentId,
            SourceDocumentVersionId = version.Id,
            Title = version.Document.Title,
            Content = generated.Content,
            Language = generated.Language,
            Status = WikiStatus.Draft,
            GeneratedByUserId = reviewerId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.WikiDrafts.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "WikiDraftGenerated", "WikiDraft", draft.Id, new { draft.SourceDocumentId, draft.SourceDocumentVersionId }, cancellationToken);
        draft.SourceDocument = version.Document;
        draft.SourceDocumentVersion = version;
        return ToDetailResponse(draft);
    }

    public async Task<IReadOnlyList<WikiDraftListItemResponse>> GetDraftsAsync(CancellationToken cancellationToken = default)
    {
        var drafts = await dbContext.WikiDrafts
            .AsNoTracking()
            .Include(draft => draft.SourceDocument)
                .ThenInclude(document => document!.Folder)
            .OrderBy(draft => draft.Status == WikiStatus.Published)
            .ThenBy(draft => draft.Id)
            .ToListAsync(cancellationToken);

        return drafts.Select(ToListResponse).ToList();
    }

    public async Task<WikiDraftDetailResponse> GetDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.WikiDrafts
            .AsNoTracking()
            .Include(item => item.SourceDocument)
                .ThenInclude(document => document!.Folder)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (draft is null)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }

        return ToDetailResponse(draft);
    }

    public async Task<WikiPageResponse> PublishAsync(Guid draftId, Guid reviewerId, PublishWikiDraftRequest request, CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.WikiDrafts
            .Include(item => item.SourceDocument)
                .ThenInclude(document => document!.Folder)
            .FirstOrDefaultAsync(item => item.Id == draftId, cancellationToken);

        if (draft?.SourceDocument is null)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }

        if (draft.Status != WikiStatus.Draft)
        {
            throw new InvalidOperationException("wiki_draft_not_publishable");
        }

        if (request.VisibilityScope == VisibilityScope.Company && !request.IsCompanyPublicConfirmed)
        {
            throw new InvalidOperationException("company_public_confirmation_required");
        }

        Guid? folderId = request.VisibilityScope == VisibilityScope.Folder
            ? request.FolderId ?? draft.SourceDocument.FolderId
            : null;

        if (folderId is not null && !await folderPermissionService.CanViewFolderAsync(reviewerId, folderId.Value, cancellationToken))
        {
            throw new UnauthorizedAccessException("folder_forbidden");
        }

        var folderPath = folderId is null
            ? string.Empty
            : await dbContext.Folders
                .AsNoTracking()
                .Where(folder => folder.Id == folderId.Value && folder.DeletedAt == null)
                .Select(folder => folder.Path)
                .FirstOrDefaultAsync(cancellationToken);

        if (folderId is not null && folderPath is null)
        {
            throw new KeyNotFoundException("folder_not_found");
        }

        var now = DateTimeOffset.UtcNow;
        var page = new WikiPageEntity
        {
            Id = Guid.NewGuid(),
            SourceDraftId = draft.Id,
            SourceDocumentId = draft.SourceDocumentId,
            SourceDocumentVersionId = draft.SourceDocumentVersionId,
            Title = draft.Title,
            Content = draft.Content,
            Language = draft.Language,
            VisibilityScope = request.VisibilityScope,
            FolderId = folderId,
            IsCompanyPublicConfirmed = request.IsCompanyPublicConfirmed,
            PublishedByUserId = reviewerId,
            PublishedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        draft.Status = WikiStatus.Published;
        draft.ReviewedByUserId = reviewerId;
        draft.ReviewedAt = now;
        draft.UpdatedAt = now;

        dbContext.WikiPages.Add(page);
        await dbContext.SaveChangesAsync(cancellationToken);

        await IndexWikiPageAsync(page, folderPath ?? string.Empty, cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "WikiPublished", "WikiPage", page.Id, new { DraftId = draft.Id, page.VisibilityScope, page.FolderId }, cancellationToken);
        return new WikiPageResponse(page.Id, page.SourceDraftId, page.Title, page.Content, page.Language, page.VisibilityScope, page.FolderId, folderPath, page.PublishedAt);
    }

    public async Task<WikiDraftDetailResponse> RejectAsync(Guid draftId, Guid reviewerId, RejectWikiDraftRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("reject_reason_required");
        }

        var draft = await dbContext.WikiDrafts
            .Include(item => item.SourceDocument)
                .ThenInclude(document => document!.Folder)
            .FirstOrDefaultAsync(item => item.Id == draftId, cancellationToken);

        if (draft is null)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }

        if (draft.Status != WikiStatus.Draft)
        {
            throw new InvalidOperationException("wiki_draft_not_rejectable");
        }

        var now = DateTimeOffset.UtcNow;
        draft.Status = WikiStatus.Rejected;
        draft.RejectReason = request.Reason.Trim();
        draft.ReviewedByUserId = reviewerId;
        draft.ReviewedAt = now;
        draft.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(reviewerId, "WikiRejected", "WikiDraft", draft.Id, new { Reason = draft.RejectReason }, cancellationToken);

        return ToDetailResponse(draft);
    }

    private async Task IndexWikiPageAsync(WikiPageEntity page, string folderPath, CancellationToken cancellationToken)
    {
        var chunks = chunker.Chunk(page.Content);
        var vectorChunks = chunks.Select(chunk => new KnowledgeChunkRecord(
            $"{page.Id:N}-{chunk.Index}",
            embeddingService.CreateEmbedding(chunk.Text),
            chunk.Text,
            new Dictionary<string, object>
            {
                ["chunk_id"] = $"{page.Id:N}-{chunk.Index}",
                ["source_type"] = "wiki",
                ["source_id"] = page.Id.ToString(),
                ["wiki_page_id"] = page.Id.ToString(),
                ["document_id"] = page.SourceDocumentId.ToString(),
                ["document_version_id"] = page.SourceDocumentVersionId.ToString(),
                ["folder_id"] = page.FolderId?.ToString() ?? string.Empty,
                ["title"] = page.Title,
                ["folder_path"] = folderPath,
                ["status"] = "published",
                ["visibility_scope"] = page.VisibilityScope == VisibilityScope.Company ? "company" : "folder",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            })).ToList();

        await vectorStore.UpsertChunksAsync(vectorChunks, cancellationToken);
    }

    private static WikiDraftListItemResponse ToListResponse(WikiDraftEntity draft)
    {
        return new WikiDraftListItemResponse(
            draft.Id,
            draft.SourceDocumentId,
            draft.SourceDocumentVersionId,
            draft.Title,
            draft.SourceDocument?.Title ?? draft.Title,
            draft.SourceDocument?.Folder?.Path ?? string.Empty,
            draft.Language,
            draft.Status,
            draft.CreatedAt,
            draft.UpdatedAt);
    }

    private static WikiDraftDetailResponse ToDetailResponse(WikiDraftEntity draft)
    {
        return new WikiDraftDetailResponse(
            draft.Id,
            draft.SourceDocumentId,
            draft.SourceDocumentVersionId,
            draft.Title,
            draft.SourceDocument?.Title ?? draft.Title,
            draft.SourceDocument?.Folder?.Path ?? string.Empty,
            draft.Content,
            draft.Language,
            draft.Status,
            draft.RejectReason,
            draft.CreatedAt,
            draft.UpdatedAt,
            draft.ReviewedAt);
    }
}
