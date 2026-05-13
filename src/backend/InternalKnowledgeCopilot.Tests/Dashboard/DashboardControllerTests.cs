using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Dashboard;

public sealed class DashboardControllerTests
{
    [Fact]
    public async Task GetSummary_UsesClientSideEvaluationRunOrdering_ForSqliteDateTimeOffset()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "admin@example.local",
            DisplayName = "Admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            MustChangePassword = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        dbContext.EvaluationRuns.AddRange(
            new EvaluationRunEntity
            {
                Id = Guid.NewGuid(),
                Name = "Older",
                TotalCases = 10,
                PassedCases = 7,
                FailedCases = 3,
                CreatedByUserId = userId,
                CreatedAt = now.AddDays(-2),
                FinishedAt = now.AddDays(-2).AddMinutes(5),
            },
            new EvaluationRunEntity
            {
                Id = Guid.NewGuid(),
                Name = "Newer",
                TotalCases = 4,
                PassedCases = 3,
                FailedCases = 1,
                CreatedByUserId = userId,
                CreatedAt = now.AddDays(-1),
                FinishedAt = now.AddDays(-1).AddMinutes(5),
            });
        await dbContext.SaveChangesAsync();

        var controller = new DashboardController(dbContext);

        var response = await controller.GetSummary(null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var summary = Assert.IsType<DashboardSummaryResponse>(ok.Value);
        Assert.Equal(4, summary.LatestEvaluationTotalCases);
        Assert.Equal(3, summary.LatestEvaluationPassedCases);
        Assert.Equal(75, summary.LatestEvaluationPassRate);
        Assert.Equal(now.AddDays(-1).AddMinutes(5), summary.LatestEvaluationRunAt);
    }
}
