using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Documents;

public sealed record DocumentListItemResponse(
    Guid Id,
    Guid FolderId,
    string FolderPath,
    string Title,
    string? Description,
    DocumentStatus Status,
    Guid? CurrentVersionId,
    int? CurrentVersionNumber,
    int LatestVersionNumber,
    int PendingVersionCount,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DocumentVersionResponse(
    Guid Id,
    int VersionNumber,
    string OriginalFileName,
    long FileSizeBytes,
    string? ContentType,
    DocumentVersionStatus Status,
    string? RejectReason,
    string UploadedBy,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

public sealed record DocumentDetailResponse(
    Guid Id,
    Guid FolderId,
    string FolderPath,
    string Title,
    string? Description,
    DocumentStatus Status,
    Guid? CurrentVersionId,
    IReadOnlyList<DocumentVersionResponse> Versions);

public sealed class UploadDocumentRequest
{
    [FromForm(Name = "folderId")]
    public Guid FolderId { get; init; }

    [FromForm(Name = "title")]
    public string Title { get; init; } = string.Empty;

    [FromForm(Name = "description")]
    public string? Description { get; init; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed class UploadDocumentVersionRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed record ReviewDocumentRequest(Guid VersionId);

public sealed record RejectDocumentRequest(Guid VersionId, string Reason);
