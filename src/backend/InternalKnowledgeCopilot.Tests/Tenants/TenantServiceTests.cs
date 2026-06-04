using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Tenants;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Tenants;

public sealed class TenantServiceTests
{
    [Fact]
    public async Task CreateTenantAsync_CreatesTenantWithNormalizedCode()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.CreateTenantAsync(
            Guid.NewGuid(),
            new CreateTenantRequest("Acme Corp", " ACME ", null));

        Assert.Equal("acme", response.Code);
        Assert.Equal("Acme Corp", response.Name);
        Assert.Equal(TenantStatus.Active, response.Status);
        Assert.Equal(1, await dbContext.Tenants.CountAsync());
    }

    [Fact]
    public async Task CreateTenantAsync_RejectsDuplicateCode()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        await service.CreateTenantAsync(
            null,
            new CreateTenantRequest("Acme Corp", "acme", null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTenantAsync(
                null,
                new CreateTenantRequest("Acme Duplicate", "ACME", null)));

        Assert.Equal("tenant_code_exists", exception.Message);
    }

    [Fact]
    public async Task UpdateTenantAsync_UpdatesNameAndStatus()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var tenant = await service.CreateTenantAsync(
            null,
            new CreateTenantRequest("Acme Corp", "acme", null));

        var updated = await service.UpdateTenantAsync(
            tenant.Id,
            Guid.NewGuid(),
            new UpdateTenantRequest("Acme Internal", TenantStatus.Suspended));

        Assert.Equal("Acme Internal", updated.Name);
        Assert.Equal(TenantStatus.Suspended, updated.Status);
    }

    [Fact]
    public async Task GetTenantsAsync_ExcludesSoftDeletedTenants()
    {
        await using var dbContext = CreateDbContext();
        var active = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = "active",
            Name = "Active",
            Status = TenantStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var deleted = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = "deleted",
            Name = "Deleted",
            Status = TenantStatus.Archived,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Tenants.AddRange(active, deleted);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var tenants = await service.GetTenantsAsync();

        var tenant = Assert.Single(tenants);
        Assert.Equal(active.Id, tenant.Id);
    }

    private static TenantService CreateService(AppDbContext dbContext)
    {
        return new TenantService(dbContext, new NoopAuditLogService());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
