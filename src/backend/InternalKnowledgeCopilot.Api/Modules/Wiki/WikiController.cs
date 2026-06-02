using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalKnowledgeCopilot.Api.Modules.Wiki;

[ApiController]
[Route("api/wiki")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
public sealed class WikiController(IWikiService wikiService, ILogger<WikiController> logger) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<WikiDraftDetailResponse>> Generate(GenerateWikiDraftRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await wikiService.GenerateDraftAsync(reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("document_version_not_found", "Không tìm thấy phiên bản tài liệu."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "document_version_not_indexed")
        {
            return BadRequest(new ApiError("document_version_not_indexed", "Phiên bản tài liệu phải được duyệt và index trước khi sinh wiki."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "extracted_text_not_found")
        {
            return BadRequest(new ApiError("extracted_text_not_found", "Không tìm thấy nội dung đã trích xuất của tài liệu."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "extracted_text_empty")
        {
            return BadRequest(new ApiError("extracted_text_empty", "Nội dung đã trích xuất của tài liệu đang trống."));
        }
        catch (InvalidOperationException ex)
        {
            return WikiGenerationFailed(request, ex);
        }
        catch (HttpRequestException ex)
        {
            return WikiGenerationFailed(request, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return WikiGenerationFailed(request, ex);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("drafts")]
    public async Task<ActionResult<IReadOnlyList<WikiDraftListItemResponse>>> GetDrafts(CancellationToken cancellationToken)
    {
        return Ok(await wikiService.GetDraftsAsync(cancellationToken));
    }

    [HttpGet("drafts/{id:guid}")]
    public async Task<ActionResult<WikiDraftDetailResponse>> GetDraft(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await wikiService.GetDraftAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("wiki_draft_not_found", "Không tìm thấy wiki draft."));
        }
    }

    [HttpPost("drafts/{id:guid}/publish")]
    public async Task<ActionResult<WikiPageResponse>> Publish(Guid id, PublishWikiDraftRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await wikiService.PublishAsync(id, reviewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException ex) when (ex.Message == "folder_not_found")
        {
            return NotFound(new ApiError("folder_not_found", "Không tìm thấy folder publish."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("wiki_draft_not_found", "Không tìm thấy wiki draft."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "company_public_confirmation_required")
        {
            return BadRequest(new ApiError("company_public_confirmation_required", "Cần xác nhận nội dung được phép public nội bộ."));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ApiError("wiki_draft_not_publishable", "Chỉ draft mới có thể publish."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("drafts/{id:guid}/reject")]
    public async Task<ActionResult<WikiDraftDetailResponse>> Reject(Guid id, RejectWikiDraftRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized(new ApiError("invalid_token", "Token không hợp lệ."));
        }

        try
        {
            return Ok(await wikiService.RejectAsync(id, reviewerId.Value, request, cancellationToken));
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiError("reject_reason_required", "Lý do reject là bắt buộc."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("wiki_draft_not_found", "Không tìm thấy wiki draft."));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ApiError("wiki_draft_not_rejectable", "Chỉ draft mới có thể reject."));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private ObjectResult WikiGenerationFailed(GenerateWikiDraftRequest request, Exception ex)
    {
        logger.LogWarning(
            ex,
            "Wiki draft generation failed for document {DocumentId} version {DocumentVersionId}.",
            request.DocumentId,
            request.DocumentVersionId);

        return StatusCode(
            StatusCodes.Status502BadGateway,
            new ApiError(
                "wiki_generation_failed",
                "Không thể sinh wiki draft vì AI provider không phản hồi hoặc cấu hình chưa hợp lệ. Vui lòng kiểm tra cấu hình AI provider."));
    }
}
