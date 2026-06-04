using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Applications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Applications;

public sealed class ApplicationServiceTests
{
    [Fact]
    public async Task CreateApplicationAsync_CreatesApplicationForTenant()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext, "acme");
        var service = CreateService(dbContext);

        var response = await service.CreateApplicationAsync(
            Guid.NewGuid(),
            new CreateApplicationRequest(
                tenant.Id,
                " CRM ",
                "Acme CRM",
                ApplicationType.Crm,
                "https://crm.example.local/",
                null));

        Assert.Equal(tenant.Id, response.TenantId);
        Assert.Equal("acme", response.TenantCode);
        Assert.Equal("crm", response.Code);
        Assert.Equal("https://crm.example.local", response.BaseUrl);
        Assert.Equal(ApplicationStatus.Active, response.Status);
    }

    [Fact]
    public async Task CreateApplicationAsync_RejectsDuplicateCodeWithinTenant()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext, "acme");
        var service = CreateService(dbContext);
        await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(tenant.Id, "crm", "Acme CRM", ApplicationType.Crm, null, null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateApplicationAsync(
                null,
                new CreateApplicationRequest(tenant.Id, "CRM", "Duplicate CRM", ApplicationType.Crm, null, null)));

        Assert.Equal("application_code_exists", exception.Message);
    }

    [Fact]
    public async Task CreateApplicationAsync_AllowsSameCodeAcrossDifferentTenants()
    {
        await using var dbContext = CreateDbContext();
        var firstTenant = await SeedTenantAsync(dbContext, "first");
        var secondTenant = await SeedTenantAsync(dbContext, "second");
        var service = CreateService(dbContext);

        await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(firstTenant.Id, "crm", "First CRM", ApplicationType.Crm, null, null));
        await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(secondTenant.Id, "crm", "Second CRM", ApplicationType.Crm, null, null));

        Assert.Equal(2, await dbContext.Applications.CountAsync());
    }

    [Fact]
    public async Task UpdateApplicationAsync_UpdatesNameTypeBaseUrlAndStatus()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext, "acme");
        var service = CreateService(dbContext);
        var application = await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(tenant.Id, "crm", "Acme CRM", ApplicationType.Crm, null, null));

        var updated = await service.UpdateApplicationAsync(
            application.Id,
            Guid.NewGuid(),
            new UpdateApplicationRequest("Acme Sales", ApplicationType.Sales, "https://sales.example.local/", ApplicationStatus.Disabled));

        Assert.Equal("Acme Sales", updated.Name);
        Assert.Equal(ApplicationType.Sales, updated.ApplicationType);
        Assert.Equal("https://sales.example.local", updated.BaseUrl);
        Assert.Equal(ApplicationStatus.Disabled, updated.Status);
    }

    [Fact]
    public async Task GetApplicationsAsync_FiltersByTenantAndExcludesSoftDeletedApplications()
    {
        await using var dbContext = CreateDbContext();
        var firstTenant = await SeedTenantAsync(dbContext, "first");
        var secondTenant = await SeedTenantAsync(dbContext, "second");
        var service = CreateService(dbContext);
        var visible = await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(firstTenant.Id, "crm", "First CRM", ApplicationType.Crm, null, null));
        var deleted = await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(firstTenant.Id, "sales", "First Sales", ApplicationType.Sales, null, null));
        await service.CreateApplicationAsync(
            null,
            new CreateApplicationRequest(secondTenant.Id, "crm", "Second CRM", ApplicationType.Crm, null, null));
        await service.DeleteApplicationAsync(deleted.Id, null);

        var applications = await service.GetApplicationsAsync(firstTenant.Id);

        var application = Assert.Single(applications);
        Assert.Equal(visible.Id, application.Id);
    }

    private static ApplicationService CreateService(AppDbContext dbContext)
    {
        return new ApplicationService(dbContext, new NoopAuditLogService());
    }

    private static async Task<TenantEntity> SeedTenantAsync(AppDbContext dbContext, string code)
    {
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{code} Tenant",
            Status = TenantStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
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
