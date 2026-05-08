using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Teams;

[ApiController]
[Route("api/teams")]
public sealed class TeamsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Reviewer)}")]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetTeams(CancellationToken cancellationToken)
    {
        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.DeletedAt == null)
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
        var exists = await dbContext.Teams.AnyAsync(team => team.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict(new ApiError("team_exists", "Team đã tồn tại."));
        }

        var now = DateTimeOffset.UtcNow;
        var team = new TeamEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TeamResponse(team.Id, team.Name, team.Description);
        return CreatedAtAction(nameof(GetTeams), response);
    }
}
