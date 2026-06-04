using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.Integrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Integrations;

public sealed class IntegrationServiceTests
{
    [Fact]
    public async Task CreateConnectionAsync_HashesSecretAndDoesNotExposeHash()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var hasher = new IntegrationSecretHasher();
        var service = CreateService(fixture.DbContext, application.TenantId, hasher);

        var response = await service.CreateConnectionAsync(
            null,
            new CreateIntegrationConnectionRequest(
                application.Id,
                "CRM Integration",
                "https://crm.example.local",
                IntegrationAuthMode.InternalApiKey,
                "crm-main",
                "secret-value",
                null,
                """{"owner":"sales"}"""));

        var entity = await fixture.DbContext.IntegrationConnections.SingleAsync();
        Assert.Equal(response.Id, entity.Id);
        Assert.True(response.SecretConfigured);
        Assert.NotEqual("secret-value", entity.SecretHash);
        Assert.True(hasher.VerifySecret("secret-value", entity.SecretHash));
        Assert.Equal("""{"owner":"sales"}""", response.MetadataJson);
    }

    [Fact]
    public async Task ReceiveDomainEventAsync_IsIdempotentByTenantApplicationAndKey()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        await CreateConnectionAsync(service, application.Id);

        var first = await service.ReceiveDomainEventAsync(
            "crm",
            new IntegrationAuthenticationRequest("crm-main", "secret-value"),
            new DomainIntegrationEventRequest(
                "deal-stage-001",
                "deal.stage.changed",
                "evt-001",
                "deal",
                "D-100",
                DateTimeOffset.UtcNow,
                """{"stage":"Proposal"}""",
                null));
        var second = await service.ReceiveDomainEventAsync(
            "crm",
            new IntegrationAuthenticationRequest("crm-main", "secret-value"),
            new DomainIntegrationEventRequest(
                "deal-stage-001",
                "deal.stage.changed",
                "evt-001-retry",
                "deal",
                "D-100",
                DateTimeOffset.UtcNow,
                """{"stage":"Proposal"}""",
                null));

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(1, await fixture.DbContext.IntegrationInboundEvents.CountAsync());
    }

    [Fact]
    public async Task ReceiveDomainEventAsync_RejectsInvalidApplicationCode()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        await CreateConnectionAsync(service, application.Id);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReceiveDomainEventAsync(
            "missing-crm",
            new IntegrationAuthenticationRequest("crm-main", "secret-value"),
            new DomainIntegrationEventRequest("evt-001", "deal.stage.changed", null, null, null, null, null, null)));

        Assert.Equal("application_not_found", exception.Message);
        Assert.Empty(fixture.DbContext.IntegrationInboundEvents);
    }

    [Fact]
    public async Task ReceiveDomainEventAsync_RejectsMissingTenantContext()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, tenantId: null);
        var tenantService = CreateService(fixture.DbContext, application.TenantId);
        await CreateConnectionAsync(tenantService, application.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveDomainEventAsync(
            "crm",
            new IntegrationAuthenticationRequest("crm-main", "secret-value"),
            new DomainIntegrationEventRequest("evt-001", "deal.stage.changed", null, null, null, null, null, null)));

        Assert.Equal("tenant_required", exception.Message);
        Assert.Empty(fixture.DbContext.IntegrationInboundEvents);
    }

    [Fact]
    public async Task ReceiveDomainEventAsync_RejectsUnauthorizedIntegrationKey()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        await CreateConnectionAsync(service, application.Id);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReceiveDomainEventAsync(
            "crm",
            new IntegrationAuthenticationRequest("crm-main", "wrong-secret"),
            new DomainIntegrationEventRequest("evt-001", "deal.stage.changed", null, null, null, null, null, null)));

        Assert.Equal("integration_unauthorized", exception.Message);
        Assert.Empty(fixture.DbContext.IntegrationInboundEvents);
    }

    private static async Task<IntegrationConnectionResponse> CreateConnectionAsync(IntegrationService service, Guid applicationId)
    {
        return await service.CreateConnectionAsync(
            null,
            new CreateIntegrationConnectionRequest(
                applicationId,
                "CRM Integration",
                "https://crm.example.local",
                IntegrationAuthMode.InternalApiKey,
                "crm-main",
                "secret-value",
                IntegrationConnectionStatus.Active,
                null));
    }

    private static IntegrationService CreateService(AppDbContext dbContext, Guid? tenantId, IIntegrationSecretHasher? secretHasher = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is not null)
        {
            tenantContext.SetTenant(tenantId.Value, "tenant");
        }

        return new IntegrationService(
            dbContext,
            tenantContext,
            new NoopAuditLogService(),
            secretHasher ?? new IntegrationSecretHasher());
    }

    private static async Task<(TenantEntity Tenant, ApplicationEntity Application)> SeedTenantAndApplicationAsync(
        AppDbContext dbContext,
        string tenantCode,
        string applicationCode)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = tenantCode,
            Name = $"{tenantCode} Tenant",
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var application = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = applicationCode,
            Name = $"{applicationCode} App",
            ApplicationType = ApplicationType.Crm,
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync();
        return (tenant, application);
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private SqliteFixture(SqliteConnection connection, AppDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }

        public AppDbContext DbContext { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new SqliteFixture(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
