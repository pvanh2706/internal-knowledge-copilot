using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Teams;

[ApiController]
[Route("api/teams")]
public sealed class TeamsController(AppDbContext dbContext, ITenantContext tenantContext, IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetTeams(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.TenantId == tenantId && team.DeletedAt == null)
            .OrderBy(team => team.Name)
            .Select(team => new TeamResponse(team.Id, team.Name, team.Description))
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<TeamResponse>> CreateTeam(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var tenantId = tenantContext.GetRequiredTenantId();
        var exists = await dbContext.Teams.AnyAsync(team => team.TenantId == tenantId && team.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict(new ApiError("team_exists", "Team đã tồn tại."));
        }

        var now = DateTimeOffset.UtcNow;
        var team = new TeamEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(GetCurrentUserId(), "TeamCreated", "Team", team.Id, new { team.Name }, cancellationToken);

        var response = new TeamResponse(team.Id, team.Name, team.Description);
        return CreatedAtAction(nameof(GetTeams), response);
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
