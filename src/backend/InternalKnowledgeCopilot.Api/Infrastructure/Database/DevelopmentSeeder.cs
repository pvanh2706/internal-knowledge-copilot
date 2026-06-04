using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
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

        var defaultTenant = await EnsureTenantAsync(
            dbContext,
            "Default Tenant",
            TenantDefaults.DefaultTenantCode,
            cancellationToken);
        await EnsureApplicationAsync(
            dbContext,
            defaultTenant.Id,
            TenantDefaults.DefaultApplicationCode,
            "Internal Knowledge Copilot",
            ApplicationType.Internal,
            null,
            cancellationToken);

        var engineeringTeam = await EnsureTeamAsync(dbContext, defaultTenant.Id, "Kỹ thuật", "Team kỹ thuật", cancellationToken);
        await EnsureTeamAsync(dbContext, defaultTenant.Id, "Hỗ trợ khách hàng", "Team hỗ trợ khách hàng", cancellationToken);

        await EnsureUserAsync(
            dbContext,
            passwordHasher,
            configuration["Seed:AdminEmail"] ?? "admin@example.local",
            "Admin",
            UserRole.Admin,
            defaultTenant.Id,
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
            defaultTenant.Id,
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
            defaultTenant.Id,
            engineeringTeam.Id,
            configuration["Seed:UserPassword"] ?? "ChangeMe123!",
            true,
            cancellationToken);
    }

    private static async Task<TenantEntity> EnsureTenantAsync(
        AppDbContext dbContext,
        string name,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToLowerInvariant();
        var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Code == normalizedCode, cancellationToken);
        if (existingTenant is not null)
        {
            return existingTenant;
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity
        {
            Id = TenantDefaults.DefaultTenantId,
            Name = name,
            Code = normalizedCode,
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private static async Task EnsureApplicationAsync(
        AppDbContext dbContext,
        Guid tenantId,
        string code,
        string name,
        ApplicationType applicationType,
        string? baseUrl,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToLowerInvariant();
        var existingApplication = await dbContext.Applications
            .FirstOrDefaultAsync(application => application.TenantId == tenantId && application.Code == normalizedCode, cancellationToken);
        if (existingApplication is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Applications.Add(new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = normalizedCode,
            Name = name,
            ApplicationType = applicationType,
            BaseUrl = baseUrl,
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<TeamEntity> EnsureTeamAsync(
        AppDbContext dbContext,
        Guid tenantId,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        var existingTeam = await dbContext.Teams.FirstOrDefaultAsync(
            team => team.TenantId == tenantId && team.Name == name,
            cancellationToken);
        if (existingTeam is not null)
        {
            return existingTeam;
        }

        var now = DateTimeOffset.UtcNow;
        var teamEntity = new TeamEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
        Guid tenantId,
        Guid teamId,
        string password,
        bool mustChangePassword,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users.FirstOrDefaultAsync(
            user => user.TenantId == tenantId && user.Email == normalizedEmail,
            cancellationToken);
        if (existingUser is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
