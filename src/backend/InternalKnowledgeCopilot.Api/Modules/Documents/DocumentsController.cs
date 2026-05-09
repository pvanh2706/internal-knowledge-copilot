using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Documents;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(
    AppDbContext dbContext,
    IFolderPermissionService folderPermissionService,
    IFileUploadValidator fileUploadValidator,
    IFileStorageService fileStorageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentListItemResponse>>> GetDocuments(
        [FromQuery] Guid? folderId,
        [FromQuery] DocumentStatus? status,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var visibleFolderIds = await folderPermissionService.GetVisibleFolderIdsAsync(userId.Value, cancellationToken);
        var query = dbContext.Documents
            .AsNoTracking()
            .Include(document => document.Folder)
            .Include(document => document.CreatedByUser)
            .Include(document => document.Versions)
            .Where(document => document.DeletedAt == null && visibleFolderIds.Contains(document.FolderId));

        if (folderId is not null)
        {
            query = query.Where(document => document.FolderId == folderId);
        }

        if (status is not null)
        {
            query = status == DocumentStatus.PendingReview
                ? query.Where(document => document.Status == DocumentStatus.PendingReview || document.Versions.Any(version => version.Status == DocumentVersionStatus.PendingReview))
                : query.Where(document => document.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(document => document.Title.Contains(normalizedKeyword) || (document.Description != null && document.Description.Contains(normalizedKeyword)));
        }

        var documents = await query
            .OrderByDescending(document => document.UpdatedAt)
            .Select(document => new DocumentListItemResponse(
                document.Id,
                document.FolderId,
                document.Folder!.Path,
                document.Title,
                document.Description,
                document.Status,
                document.CurrentVersionId,
                document.CurrentVersionId == null ? null : document.Versions.Where(version => version.Id == document.CurrentVersionId).Select(version => (int?)version.VersionNumber).FirstOrDefault(),
                document.Versions.Max(version => version.VersionNumber),
                document.Versions.Count(version => version.Status == DocumentVersionStatus.PendingReview),
                document.CreatedByUser!.DisplayName,
                document.CreatedAt,
                document.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    [HttpPost]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDetailResponse>> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var validation = fileUploadValidator.Validate(request.File);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiError(validation.ErrorCode!, validation.Message!));
        }

        if (!await folderPermissionService.CanViewFolderAsync(userId.Value, request.FolderId, cancellationToken))
        {
            return Forbid();
        }

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new ApiError("title_required", "Tên tài liệu là bắt buộc."));
        }

        var now = DateTimeOffset.UtcNow;
        var document = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            FolderId = request.FolderId,
            Title = title,
            Description = request.Description?.Trim(),
            Status = DocumentStatus.PendingReview,
            CreatedByUserId = userId.Value,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var version = await CreateVersionAsync(document.Id, 1, request.File!, userId.Value, now, cancellationToken);
        dbContext.Documents.Add(document);
        dbContext.DocumentVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, await BuildDetailResponseAsync(document.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/versions")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDetailResponse>> UploadVersion(Guid id, [FromForm] UploadDocumentVersionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var document = await dbContext.Documents
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (document is null)
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }

        if (!await folderPermissionService.CanViewFolderAsync(userId.Value, document.FolderId, cancellationToken))
        {
            return Forbid();
        }

        var validation = fileUploadValidator.Validate(request.File);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiError(validation.ErrorCode!, validation.Message!));
        }

        var nextVersionNumber = document.Versions.Count == 0 ? 1 : document.Versions.Max(version => version.VersionNumber) + 1;
        var now = DateTimeOffset.UtcNow;
        var version = await CreateVersionAsync(document.Id, nextVersionNumber, request.File!, userId.Value, now, cancellationToken);
        dbContext.DocumentVersions.Add(version);
        if (document.CurrentVersionId is null)
        {
            document.Status = DocumentStatus.PendingReview;
        }

        document.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildDetailResponseAsync(document.Id, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetailResponse>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (document is null)
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }

        if (!await folderPermissionService.CanViewFolderAsync(userId.Value, document.FolderId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await BuildDetailResponseAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] Guid? versionId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (document is null)
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }

        if (!await folderPermissionService.CanViewFolderAsync(userId.Value, document.FolderId, cancellationToken))
        {
            return Forbid();
        }

        var targetVersionId = versionId ?? document.CurrentVersionId;
        if (targetVersionId is null)
        {
            return BadRequest(new ApiError("no_current_version", "Tài liệu chưa có phiên bản được duyệt."));
        }

        var version = await dbContext.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == id && item.Id == targetVersionId, cancellationToken);

        if (version is null)
        {
            return NotFound(new ApiError("version_not_found", "Không tìm thấy phiên bản tài liệu."));
        }

        if (!System.IO.File.Exists(version.StoredFilePath))
        {
            return NotFound(new ApiError("file_not_found", "Không tìm thấy file gốc."));
        }

        return PhysicalFile(version.StoredFilePath, version.ContentType ?? "application/octet-stream", version.OriginalFileName);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = nameof(UserRole.Reviewer))]
    public async Task<IActionResult> Approve(Guid id, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        var document = await dbContext.Documents
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (document is null)
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }

        var version = document.Versions.FirstOrDefault(item => item.Id == request.VersionId);
        if (version is null)
        {
            return NotFound(new ApiError("version_not_found", "Không tìm thấy phiên bản tài liệu."));
        }

        var now = DateTimeOffset.UtcNow;
        version.Status = DocumentVersionStatus.Approved;
        version.RejectReason = null;
        version.ReviewedByUserId = reviewerId.Value;
        version.ReviewedAt = now;
        version.UpdatedAt = now;
        document.CurrentVersionId = version.Id;
        document.Status = DocumentStatus.Approved;
        document.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = nameof(UserRole.Reviewer))]
    public async Task<IActionResult> Reject(Guid id, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiError("reject_reason_required", "Lý do reject là bắt buộc."));
        }

        var document = await dbContext.Documents
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (document is null)
        {
            return NotFound(new ApiError("document_not_found", "Không tìm thấy tài liệu."));
        }

        var version = document.Versions.FirstOrDefault(item => item.Id == request.VersionId);
        if (version is null)
        {
            return NotFound(new ApiError("version_not_found", "Không tìm thấy phiên bản tài liệu."));
        }

        var now = DateTimeOffset.UtcNow;
        version.Status = DocumentVersionStatus.Rejected;
        version.RejectReason = request.Reason.Trim();
        version.ReviewedByUserId = reviewerId.Value;
        version.ReviewedAt = now;
        version.UpdatedAt = now;

        if (document.CurrentVersionId is null)
        {
            document.Status = DocumentStatus.Rejected;
        }

        document.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<DocumentVersionEntity> CreateVersionAsync(Guid documentId, int versionNumber, IFormFile file, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var versionId = Guid.NewGuid();
        var storedPath = await fileStorageService.SaveDocumentVersionAsync(documentId, versionId, file, cancellationToken);

        return new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            VersionNumber = versionNumber,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFilePath = storedPath,
            FileExtension = Path.GetExtension(file.FileName).ToLowerInvariant(),
            FileSizeBytes = file.Length,
            ContentType = file.ContentType,
            Status = DocumentVersionStatus.PendingReview,
            UploadedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private async Task<DocumentDetailResponse> BuildDetailResponseAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .AsNoTracking()
            .Include(item => item.Folder)
            .Include(item => item.Versions)
                .ThenInclude(version => version.UploadedByUser)
            .Include(item => item.Versions)
                .ThenInclude(version => version.ReviewedByUser)
            .FirstAsync(item => item.Id == documentId, cancellationToken);

        var versions = document.Versions
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new DocumentVersionResponse(
                version.Id,
                version.VersionNumber,
                version.OriginalFileName,
                version.FileSizeBytes,
                version.ContentType,
                version.Status,
                version.RejectReason,
                version.UploadedByUser!.DisplayName,
                version.ReviewedByUser?.DisplayName,
                version.ReviewedAt,
                version.CreatedAt))
            .ToList();

        return new DocumentDetailResponse(
            document.Id,
            document.FolderId,
            document.Folder!.Path,
            document.Title,
            document.Description,
            document.Status,
            document.CurrentVersionId,
            versions);
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
