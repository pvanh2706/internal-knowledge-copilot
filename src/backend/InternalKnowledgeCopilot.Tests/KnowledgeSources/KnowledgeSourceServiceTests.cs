using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.KnowledgeSources;

public sealed class KnowledgeSourceServiceTests
{
    [Fact]
    public async Task UpsertSourceAsync_IsIdempotentByTenantApplicationTypeAndExternalId()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);

        var first = await service.UpsertSourceAsync(
            null,
            new UpsertKnowledgeSourceRequest(
                application.Id,
                KnowledgeSourceKind.External,
                "crm-docs",
                "CRM Docs",
                KnowledgeSourceSyncMode.EventDriven,
                KnowledgeSourceStatus.Active,
                """{"version":1}""",
                null,
                null,
                null,
                null));
        var second = await service.UpsertSourceAsync(
            null,
            new UpsertKnowledgeSourceRequest(
                application.Id,
                KnowledgeSourceKind.External,
                "crm-docs",
                "CRM Documents",
                KnowledgeSourceSyncMode.Scheduled,
                KnowledgeSourceStatus.Syncing,
                """{"version":2}""",
                DateTimeOffset.UtcNow,
                null,
                "running",
                null));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("CRM Documents", second.Name);
        Assert.Equal(KnowledgeSourceSyncMode.Scheduled, second.SyncMode);
        Assert.Equal(KnowledgeSourceStatus.Syncing, second.Status);
        Assert.Equal("""{"version":2}""", second.MetadataJson);
        Assert.Equal(1, await fixture.DbContext.KnowledgeSources.CountAsync());
    }

    [Fact]
    public async Task UpsertExternalObjectAsync_IsIdempotentByNaturalKey()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        var source = await service.UpsertSourceAsync(
            null,
            new UpsertKnowledgeSourceRequest(
                application.Id,
                KnowledgeSourceKind.External,
                "crm-docs",
                "CRM Docs",
                KnowledgeSourceSyncMode.EventDriven,
                null,
                null,
                null,
                null,
                null,
                null));

        var first = await service.UpsertExternalObjectAsync(
            null,
            new UpsertExternalObjectRequest(
                application.Id,
                source.Id,
                "Account",
                "ACC-001",
                "Acme Account",
                "https://crm.example.local/accounts/ACC-001",
                """{"tier":"gold"}""",
                "content-v1",
                "acl-v1",
                ExternalObjectStatus.Active,
                null,
                null,
                null));
        var second = await service.UpsertExternalObjectAsync(
            null,
            new UpsertExternalObjectRequest(
                application.Id,
                source.Id,
                "account",
                "ACC-001",
                "Acme Account Updated",
                "https://crm.example.local/accounts/ACC-001",
                """{"tier":"platinum"}""",
                "content-v2",
                "acl-v2",
                ExternalObjectStatus.Active,
                null,
                DateTimeOffset.UtcNow,
                null));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("account", second.ObjectType);
        Assert.Equal("Acme Account Updated", second.Title);
        Assert.Equal("content-v2", second.ContentHash);
        Assert.Equal("""{"tier":"platinum"}""", second.MetadataJson);
        Assert.Equal(1, await fixture.DbContext.ExternalObjects.CountAsync());
    }

    [Fact]
    public async Task ReplaceAclSnapshotsAsync_ReplacesOnlyTheTargetObjectSnapshots()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        await service.UpsertExternalObjectAsync(
            null,
            new UpsertExternalObjectRequest(
                application.Id,
                null,
                "ticket",
                "T-001",
                "Payment ticket",
                null,
                null,
                null,
                null,
                ExternalObjectStatus.Active,
                null,
                null,
                null));

        var first = await service.ReplaceAclSnapshotsAsync(
            null,
            new ReplaceExternalAclSnapshotsRequest(
                application.Id,
                "ticket",
                "T-001",
                [
                    new ExternalAclSnapshotItemRequest("team", "support", "Support", ExternalAclPermission.View, null, null, null),
                    new ExternalAclSnapshotItemRequest("user", "agent-1", "Agent 1", ExternalAclPermission.Edit, null, null, null),
                ]));
        var second = await service.ReplaceAclSnapshotsAsync(
            null,
            new ReplaceExternalAclSnapshotsRequest(
                application.Id,
                "ticket",
                "T-001",
                [
                    new ExternalAclSnapshotItemRequest("team", "support", "Support", ExternalAclPermission.Owner, null, null, """{"source":"crm"}"""),
                ]));

        Assert.Equal(2, first.Count);
        var snapshot = Assert.Single(second);
        Assert.Equal("team", snapshot.SubjectType);
        Assert.Equal("support", snapshot.SubjectId);
        Assert.Equal(ExternalAclPermission.Owner, snapshot.Permission);
        Assert.Equal("""{"source":"crm"}""", snapshot.MetadataJson);
        Assert.Equal(1, await fixture.DbContext.ExternalAclSnapshots.CountAsync());
        Assert.NotNull(await fixture.DbContext.ExternalObjects.Select(item => item.AclSyncedAt).SingleAsync());
    }

    [Fact]
    public async Task ReplaceAclSnapshotsAsync_RejectsDuplicatesBeforeDeletingExistingSnapshots()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (_, application) = await SeedTenantAndApplicationAsync(fixture.DbContext, "acme", "crm");
        var service = CreateService(fixture.DbContext, application.TenantId);
        await service.UpsertExternalObjectAsync(
            null,
            new UpsertExternalObjectRequest(
                application.Id,
                null,
                "ticket",
                "T-001",
                "Payment ticket",
                null,
                null,
                null,
                null,
                ExternalObjectStatus.Active,
                null,
                null,
                null));
        await service.ReplaceAclSnapshotsAsync(
            null,
            new ReplaceExternalAclSnapshotsRequest(
                application.Id,
                "ticket",
                "T-001",
                [
                    new ExternalAclSnapshotItemRequest("team", "support", "Support", ExternalAclPermission.View, null, null, null),
                ]));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAclSnapshotsAsync(
            null,
            new ReplaceExternalAclSnapshotsRequest(
                application.Id,
                "ticket",
                "T-001",
                [
                    new ExternalAclSnapshotItemRequest("team", "support", "Support", ExternalAclPermission.View, null, null, null),
                    new ExternalAclSnapshotItemRequest("Team", "support", "Support", ExternalAclPermission.View, null, null, null),
                ])));

        Assert.Equal("duplicate_acl_snapshot", exception.Message);
        Assert.Equal(1, await fixture.DbContext.ExternalAclSnapshots.CountAsync());
    }

    private static KnowledgeSourceService CreateService(AppDbContext dbContext, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant");
        return new KnowledgeSourceService(dbContext, tenantContext, new NoopAuditLogService());
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
