using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Seed:Enabled", true))
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var engineeringTeam = await EnsureTeamAsync(dbContext, "Kỹ thuật", "Team kỹ thuật", cancellationToken);
        await EnsureTeamAsync(dbContext, "Hỗ trợ khách hàng", "Team hỗ trợ khách hàng", cancellationToken);

        await EnsureUserAsync(
            dbContext,
            passwordHasher,
            configuration["Seed:AdminEmail"] ?? "admin@example.local",
            "Admin",
            UserRole.Admin,
            engineeringTeam.Id,
            configuration["Seed:AdminPassword"] ?? "ChangeMe123!",
            false,
            cancellationToken);

        await EnsureUserAsync(
            dbContext,
            passwordHasher,
            configuration["Seed:ReviewerEmail"] ?? "reviewer@example.local",
            "Reviewer",
            UserRole.Reviewer,
            engineeringTeam.Id,
            configuration["Seed:ReviewerPassword"] ?? "ChangeMe123!",
            false,
            cancellationToken);

        await EnsureUserAsync(
            dbContext,
            passwordHasher,
            configuration["Seed:UserEmail"] ?? "user@example.local",
            "User",
            UserRole.User,
            engineeringTeam.Id,
            configuration["Seed:UserPassword"] ?? "ChangeMe123!",
            true,
            cancellationToken);
    }

    private static async Task<TeamEntity> EnsureTeamAsync(AppDbContext dbContext, string name, string description, CancellationToken cancellationToken)
    {
        var existingTeam = await dbContext.Teams.FirstOrDefaultAsync(team => team.Name == name, cancellationToken);
        if (existingTeam is not null)
        {
            return existingTeam;
        }

        var now = DateTimeOffset.UtcNow;
        var teamEntity = new TeamEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Teams.Add(teamEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return teamEntity;
    }

    private static async Task EnsureUserAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        string email,
        string displayName,
        UserRole role,
        Guid teamId,
        string password,
        bool mustChangePassword,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = displayName,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role,
            PrimaryTeamId = teamId,
            MustChangePassword = mustChangePassword,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
