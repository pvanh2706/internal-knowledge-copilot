using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Wiki;

public sealed class WikiControllerTests
{
    [Fact]
    public async Task Generate_ReturnsWikiGenerationFailed_WhenProviderThrows()
    {
        var controller = CreateController(new ThrowingWikiService("AI provider request failed with HTTP 403."));

        var result = await controller.Generate(
            new GenerateWikiDraftRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        var error = Assert.IsType<ApiError>(objectResult.Value);
        Assert.Equal("wiki_generation_failed", error.Error);
    }

    [Fact]
    public async Task Generate_ReturnsWikiGenerationFailed_WhenProviderTimesOut()
    {
        var controller = CreateController(new ThrowingWikiService(new TaskCanceledException("provider timeout")));

        var result = await controller.Generate(
            new GenerateWikiDraftRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        var error = Assert.IsType<ApiError>(objectResult.Value);
        Assert.Equal("wiki_generation_failed", error.Error);
    }

    [Fact]
    public async Task Generate_ReturnsExtractedTextNotFound_WhenSourceTextIsMissing()
    {
        var controller = CreateController(new ThrowingWikiService("extracted_text_not_found"));

        var result = await controller.Generate(
            new GenerateWikiDraftRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("extracted_text_not_found", error.Error);
    }

    private static WikiController CreateController(IWikiService wikiService)
    {
        var controller = new WikiController(wikiService, NullLogger<WikiController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                ], "test")),
            },
        };

        return controller;
    }

    private sealed class ThrowingWikiService(Exception exception) : IWikiService
    {
        public ThrowingWikiService(string message)
            : this(new InvalidOperationException(message))
        {
        }

        public Task<WikiDraftDetailResponse> GenerateDraftAsync(Guid reviewerId, GenerateWikiDraftRequest request, CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task<IReadOnlyList<WikiDraftListItemResponse>> GetDraftsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WikiDraftListItemResponse>>([]);
        }

        public Task<WikiDraftDetailResponse> GetDraftAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }

        public Task<WikiPageResponse> PublishAsync(Guid draftId, Guid reviewerId, PublishWikiDraftRequest request, CancellationToken cancellationToken = default)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }

        public Task<WikiDraftDetailResponse> RejectAsync(Guid draftId, Guid reviewerId, RejectWikiDraftRequest request, CancellationToken cancellationToken = default)
        {
            throw new KeyNotFoundException("wiki_draft_not_found");
        }
    }
}
