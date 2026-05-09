using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Wiki;

public sealed record GenerateWikiDraftRequest(Guid DocumentId, Guid DocumentVersionId);

public sealed record PublishWikiDraftRequest(VisibilityScope VisibilityScope, Guid? FolderId, bool IsCompanyPublicConfirmed);

public sealed record RejectWikiDraftRequest(string Reason);

public sealed record WikiDraftListItemResponse(
    Guid Id,
    Guid SourceDocumentId,
    Guid SourceDocumentVersionId,
    string Title,
    string SourceDocumentTitle,
    string FolderPath,
    string Language,
    WikiStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WikiDraftDetailResponse(
    Guid Id,
    Guid SourceDocumentId,
    Guid SourceDocumentVersionId,
    string Title,
    string SourceDocumentTitle,
    string FolderPath,
    string Content,
    string Language,
    WikiStatus Status,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record WikiPageResponse(
    Guid Id,
    Guid SourceDraftId,
    string Title,
    string Content,
    string Language,
    VisibilityScope VisibilityScope,
    Guid? FolderId,
    string? FolderPath,
    DateTimeOffset PublishedAt);
