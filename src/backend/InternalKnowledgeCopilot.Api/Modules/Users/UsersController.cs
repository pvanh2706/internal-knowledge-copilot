using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Users;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class UsersController(AppDbContext dbContext, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListItemResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Include(user => user.PrimaryTeam)
            .Where(user => user.DeletedAt == null)
            .OrderBy(user => user.Email)
            .Select(user => new UserListItemResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role,
                user.PrimaryTeamId,
                user.PrimaryTeam == null ? null : user.PrimaryTeam.Name,
                user.MustChangePassword,
                user.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict(new ApiError("email_exists", "Email đã tồn tại."));
        }

        if (request.PrimaryTeamId is not null)
        {
            var teamExists = await dbContext.Teams.AnyAsync(team => team.Id == request.PrimaryTeamId && team.DeletedAt == null, cancellationToken);
            if (!teamExists)
            {
                return BadRequest(new ApiError("team_not_found", "Team không tồn tại."));
            }
        }

        var now = DateTimeOffset.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.InitialPassword),
            Role = request.Role,
            PrimaryTeamId = request.PrimaryTeamId,
            MustChangePassword = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new UserListItemResponse(user.Id, user.Email, user.DisplayName, user.Role, user.PrimaryTeamId, null, user.MustChangePassword, user.IsActive);
        return CreatedAtAction(nameof(GetUsers), response);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiError("user_not_found", "Không tìm thấy user."));
        }

        if (request.PrimaryTeamId is not null)
        {
            var teamExists = await dbContext.Teams.AnyAsync(team => team.Id == request.PrimaryTeamId && team.DeletedAt == null, cancellationToken);
            if (!teamExists)
            {
                return BadRequest(new ApiError("team_not_found", "Team không tồn tại."));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        if (request.Role is not null)
        {
            user.Role = request.Role.Value;
        }

        user.PrimaryTeamId = request.PrimaryTeamId;
        user.IsActive = request.IsActive ?? user.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
