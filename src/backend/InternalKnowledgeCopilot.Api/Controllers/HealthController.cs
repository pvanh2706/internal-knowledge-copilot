using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(
    AppDbContext dbContext,
    IOptions<AppStorageOptions> storageOptions,
    IOptions<ChromaOptions> chromaOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return Ok(new HealthResponse(
            "Healthy",
            canConnect,
            storageOptions.Value.RootPath,
            chromaOptions.Value.BaseUrl,
            chromaOptions.Value.Collection));
    }
}

public sealed record HealthResponse(
    string Status,
    bool DatabaseCanConnect,
    string StorageRoot,
    string ChromaBaseUrl,
    string ChromaCollection);
