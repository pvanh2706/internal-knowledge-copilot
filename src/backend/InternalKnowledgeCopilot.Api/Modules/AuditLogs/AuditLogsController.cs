using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.AuditLogs;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AuditLogsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>> GetLogs(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Include(log => log.ActorUser)
            .AsQueryable();

        if (from is not null)
        {
            query = query.Where(log => log.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(log => log.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(log => log.EntityType == entityType.Trim());
        }

        var logs = await query
            .OrderByDescending(log => log.Id)
            .Take(100)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.ActorUserId,
                log.ActorUser == null ? null : log.ActorUser.DisplayName,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.MetadataJson,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}
