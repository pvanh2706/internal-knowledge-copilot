using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InternalKnowledgeCopilot.Tests.Platform;

// S01-A: kiểm chứng schema platform trên SQLite thật qua migration (không dùng EnsureCreated/InMemory,
// vì trigger append-only/singleton chỉ được tạo bởi migration).
public sealed class PlatformDatabaseFoundationTests
{
    private const string PreviousMigration = "20260605010352_ProductHardeningPhase10";
    private const string PlatformMigration = "20261030070953_AddPlatformDatabaseFoundation";
    private const string ValidPasswordHash = "$pbkdf2-sha512$v=1$i=600000$c2FsdA$aGFzaA";

    private static readonly string[] PlatformTables = ["platform_admins", "platform_audit_logs", "platform_security_state"];

    private static readonly string[] PlatformTriggers =
    [
        "trg_platform_admins_no_delete",
        "trg_platform_audit_logs_no_delete",
        "trg_platform_audit_logs_no_update",
        "trg_platform_security_state_guard_update",
        "trg_platform_security_state_no_delete",
    ];

    [Fact]
    public async Task Migrate_CleanDatabase_CreatesPlatformSchemaWithoutAccountsOrBootstrapState()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();

        Assert.Equal(PlatformTables, await fixture.QueryStringsAsync("SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'platform_%' ORDER BY name"));
        Assert.Equal(PlatformTriggers, await fixture.QueryStringsAsync("SELECT name FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'trg_platform_%' ORDER BY name"));
        foreach (var table in PlatformTables)
        {
            Assert.Equal(0L, await fixture.ScalarAsync($"SELECT COUNT(*) FROM \"{table}\""));
        }

        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains(PlatformMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Migrate_ReRun_DoesNotDuplicateObjects()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var before = await fixture.QueryStringsAsync("SELECT type || ':' || name FROM sqlite_master ORDER BY 1");

        await using (var context = fixture.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(before, await fixture.QueryStringsAsync("SELECT type || ':' || name FROM sqlite_master ORDER BY 1"));
        Assert.Equal(1L, await fixture.ScalarAsync($"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '{PlatformMigration}'"));
    }

    [Fact]
    public async Task Migrate_FromPreviousMigration_LeavesExistingTenantDataUnchanged()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync(PreviousMigration);
        await SeedTenantDataAsync(fixture);
        var before = await DumpTenantTablesAsync(fixture);

        await using (var context = fixture.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(before, await DumpTenantTablesAsync(fixture));
        Assert.Equal(PlatformTables, await fixture.QueryStringsAsync("SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'platform_%' ORDER BY name"));
        Assert.Empty(await fixture.QueryStringsAsync("PRAGMA foreign_key_check"));
    }

    [Fact]
    public async Task Model_IsConsistentWithSnapshot_AndPlatformEntitiesAreOutsideTenantScope()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        await using var context = fixture.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
        var designModel = context.GetService<IDesignTimeModel>().Model;
        foreach (var clrType in new[] { typeof(PlatformAdminEntity), typeof(PlatformAuditLogEntity), typeof(PlatformSecurityStateEntity) })
        {
            var entityType = designModel.FindEntityType(clrType)!;
            Assert.Null(entityType.GetQueryFilter());
            Assert.Null(entityType.FindProperty("TenantId"));
            Assert.Empty(entityType.GetSeedData());
        }

        Assert.Null(designModel.FindEntityType(typeof(PlatformAdminEntity))!.GetForeignKeys().SingleOrDefault());
        AssertCheckMatchesEnum<PlatformAdminStatus>(designModel, typeof(PlatformAdminEntity), "CK_platform_admins_status");
        AssertCheckMatchesEnum<PlatformAuditOutcome>(designModel, typeof(PlatformAuditLogEntity), "CK_platform_audit_logs_outcome");
        AssertCheckMatchesEnum<PlatformBootstrapStatus>(designModel, typeof(PlatformSecurityStateEntity), "CK_platform_security_state_bootstrap");
    }

    [Fact]
    public async Task PlatformAdmin_DuplicateNormalizedEmail_IsRejected()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        await fixture.AddAsync(NewAdmin("ops@example.com"));

        await AssertSqliteRejectsAsync(
            () => fixture.AddAsync(NewAdmin("ops@example.com")),
            "UNIQUE constraint failed: platform_admins.email_normalized");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_admins"));
    }

    [Theory]
    [InlineData("Ops@Example.com", ValidPasswordHash, "CK_platform_admins_email_normalized")]
    [InlineData(" ops@example.com", ValidPasswordHash, "CK_platform_admins_email_normalized")]
    [InlineData("no-at-sign", ValidPasswordHash, "CK_platform_admins_email_normalized")]
    [InlineData("ops@example.com", "PlainTextPassword1!", "CK_platform_admins_password_hash_versioned")]
    [InlineData("ops@example.com", "$pbkdf2-sha512$i=600000$c2FsdA$aGFzaA", "CK_platform_admins_password_hash_versioned")]
    public async Task PlatformAdmin_InvalidEmailOrUnversionedHash_IsRejected(string email, string passwordHash, string constraint)
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = NewAdmin("placeholder@example.com");
        admin.EmailNormalized = email;
        admin.PasswordHash = passwordHash;

        await AssertSqliteRejectsAsync(() => fixture.AddAsync(admin), constraint);
        Assert.Equal(0L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_admins"));
    }

    [Fact]
    public async Task PlatformAdmin_UnknownStatusOrNegativeCounters_AreRejected()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));

        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync($"UPDATE platform_admins SET status = 'SuperAdmin' WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'"),
            "CK_platform_admins_status");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync($"UPDATE platform_admins SET failed_login_count = -1 WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'"),
            "CK_platform_admins_failed_login_count");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync($"UPDATE platform_admins SET token_version = -1 WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'"),
            "CK_platform_admins_token_version");
    }

    [Fact]
    public async Task PlatformAdmin_HardDelete_IsForbidden()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));

        await AssertSqliteRejectsAsync(
            async () =>
            {
                await using var context = fixture.CreateContext();
                context.PlatformAdmins.Remove(await context.PlatformAdmins.SingleAsync(item => item.Id == admin.Id));
                await context.SaveChangesAsync();
            },
            "platform_admins_delete_forbidden");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_admins"));
    }

    [Fact]
    public async Task PlatformAdmin_RoundTripsNullableFieldsAndEnumsAsStrings()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var lockedUntil = new DateTimeOffset(2026, 10, 30, 8, 30, 0, TimeSpan.Zero);
        var withValues = NewAdmin("locked@example.com");
        withValues.Status = PlatformAdminStatus.Locked;
        withValues.FailedLoginCount = 5;
        withValues.LockedUntil = lockedUntil;
        withValues.LastLoginAt = lockedUntil.AddDays(-1);
        withValues.MustChangePassword = false;
        withValues.TokenVersion = 3;
        await fixture.AddAsync(withValues);
        var withNulls = await fixture.AddAsync(NewAdmin("new@example.com"));

        await using var context = fixture.CreateContext();
        var storedLocked = await context.PlatformAdmins.AsNoTracking().SingleAsync(item => item.Id == withValues.Id);
        var storedNew = await context.PlatformAdmins.AsNoTracking().SingleAsync(item => item.Id == withNulls.Id);

        Assert.Equal(PlatformAdminStatus.Locked, storedLocked.Status);
        Assert.Equal(5, storedLocked.FailedLoginCount);
        Assert.Equal(lockedUntil, storedLocked.LockedUntil);
        Assert.Equal(lockedUntil.AddDays(-1), storedLocked.LastLoginAt);
        Assert.False(storedLocked.MustChangePassword);
        Assert.Equal(3, storedLocked.TokenVersion);
        Assert.Equal(PlatformAdminStatus.Active, storedNew.Status);
        Assert.Null(storedNew.LockedUntil);
        Assert.Null(storedNew.LastLoginAt);
        Assert.True(storedNew.MustChangePassword);
        Assert.Equal(
            new[] { "Active", "Locked" },
            await fixture.QueryStringsAsync("SELECT status FROM platform_admins ORDER BY status"));
    }

    [Fact]
    public async Task SecurityState_IsDatabaseEnforcedSingleton()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        await fixture.AddAsync(NewState());

        var second = NewState();
        second.Id = 2;
        await AssertSqliteRejectsAsync(() => fixture.AddAsync(second), "CK_platform_security_state_singleton");
        await AssertSqliteRejectsAsync(() => fixture.AddAsync(NewState()), "UNIQUE constraint failed: platform_security_state.id");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_security_state"));
    }

    [Fact]
    public async Task SecurityState_BootstrapFieldsMustMatchStatus()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));

        var notStartedWithAdmin = NewState();
        notStartedWithAdmin.BootstrapAdminId = admin.Id;
        await AssertSqliteRejectsAsync(() => fixture.AddAsync(notStartedWithAdmin), "CK_platform_security_state_bootstrap");

        var completedWithoutAdmin = NewState();
        completedWithoutAdmin.BootstrapStatus = PlatformBootstrapStatus.Completed;
        completedWithoutAdmin.BootstrapCompletedAt = DateTimeOffset.UtcNow;
        await AssertSqliteRejectsAsync(() => fixture.AddAsync(completedWithoutAdmin), "CK_platform_security_state_bootstrap");

        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync(
                "INSERT INTO platform_security_state (id, deployment_id, bootstrap_status, state_version, created_at, updated_at) " +
                "VALUES (1, 'D1F0A8C4-0000-4000-8000-000000000001', 'Reset', 0, '2026-10-30', '2026-10-30')"),
            "CK_platform_security_state_bootstrap");
        Assert.Equal(0L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_security_state"));
    }

    [Fact]
    public async Task SecurityState_CompletedBootstrapCannotBeReversedDeletedOrReassigned()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));
        var other = await fixture.AddAsync(NewAdmin("other@example.com"));
        var state = await CompleteBootstrapAsync(fixture, admin.Id);

        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("UPDATE platform_security_state SET bootstrap_status = 'NotStarted', bootstrap_admin_id = NULL, bootstrap_completed_at = NULL, state_version = state_version + 1"),
            "platform_bootstrap_completed_immutable");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync($"UPDATE platform_security_state SET bootstrap_admin_id = '{other.Id.ToString().ToUpperInvariant()}', state_version = state_version + 1"),
            "platform_bootstrap_completed_immutable");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("UPDATE platform_security_state SET deployment_id = 'D1F0A8C4-0000-4000-8000-0000000000FF', state_version = state_version + 1"),
            "platform_security_state_identity_immutable");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("DELETE FROM platform_security_state"),
            "platform_security_state_delete_forbidden");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync($"DELETE FROM platform_admins WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'"),
            "platform_admins_delete_forbidden");

        await using var context = fixture.CreateContext();
        var stored = await context.PlatformSecurityState.AsNoTracking().SingleAsync();
        Assert.Equal(PlatformBootstrapStatus.Completed, stored.BootstrapStatus);
        Assert.Equal(admin.Id, stored.BootstrapAdminId);
        Assert.Equal(state.DeploymentId, stored.DeploymentId);
        Assert.Equal(state.StateVersion, stored.StateVersion);
    }

    [Fact]
    public async Task SecurityState_StaleOrSkippedVersionUpdate_IsRejected()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        await fixture.AddAsync(NewState());
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstState = await first.PlatformSecurityState.SingleAsync();
        var secondState = await second.PlatformSecurityState.SingleAsync();

        firstState.StateVersion += 1;
        firstState.UpdatedAt = DateTimeOffset.UtcNow;
        await first.SaveChangesAsync();
        secondState.StateVersion += 1;
        secondState.UpdatedAt = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("UPDATE platform_security_state SET state_version = state_version + 2"),
            "platform_security_state_version_conflict");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("UPDATE platform_security_state SET updated_at = '2026-10-31'"),
            "platform_security_state_version_conflict");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT state_version FROM platform_security_state"));
    }

    [Fact]
    public async Task Bootstrap_AdminStateAndAuditCommitTogetherOrNotAtAll()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();

        await AssertSqliteRejectsAsync(
            async () =>
            {
                await using var context = fixture.CreateContext();
                var admin = NewAdmin("first@example.com");
                context.PlatformAdmins.Add(admin);
                context.PlatformSecurityState.Add(NewCompletedState(admin.Id));
                context.PlatformAuditLogs.Add(NewAudit(admin.Id, metadataJson: "not-json"));
                await context.SaveChangesAsync();
            },
            "CK_platform_audit_logs_metadata_json");
        foreach (var table in PlatformTables)
        {
            Assert.Equal(0L, await fixture.ScalarAsync($"SELECT COUNT(*) FROM \"{table}\""));
        }

        Guid firstAdminId;
        await using (var context = fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var admin = NewAdmin("first@example.com");
            firstAdminId = admin.Id;
            context.PlatformAdmins.Add(admin);
            context.PlatformSecurityState.Add(NewCompletedState(admin.Id));
            context.PlatformAuditLogs.Add(NewAudit(admin.Id, metadataJson: """{"method":"cli"}"""));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await AssertSqliteRejectsAsync(
            async () =>
            {
                await using var context = fixture.CreateContext();
                var admin = NewAdmin("second@example.com");
                context.PlatformAdmins.Add(admin);
                context.PlatformSecurityState.Add(NewCompletedState(admin.Id));
                await context.SaveChangesAsync();
            },
            "UNIQUE constraint failed: platform_security_state.id");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_admins"));
        Assert.Equal(firstAdminId.ToString().ToUpperInvariant(), (await fixture.QueryStringsAsync("SELECT bootstrap_admin_id FROM platform_security_state")).Single());
    }

    [Fact]
    public async Task AuditLog_IsAppendOnly()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));
        await fixture.AddAsync(NewAudit(admin.Id, metadataJson: null));

        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("UPDATE platform_audit_logs SET outcome = 'Success'"),
            "platform_audit_logs_append_only");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("DELETE FROM platform_audit_logs"),
            "platform_audit_logs_append_only");
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_audit_logs"));
    }

    [Fact]
    public async Task AuditLog_ForeignKeysAreEnforcedWithProductionConnectionString()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"s01a-fk-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath}").Options;
            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            var unknownActor = NewAudit(Guid.NewGuid(), metadataJson: null);
            var unknownTenant = NewAudit(null, metadataJson: null);
            unknownTenant.TargetTenantId = Guid.NewGuid();
            foreach (var audit in new[] { unknownActor, unknownTenant })
            {
                await AssertSqliteRejectsAsync(
                    async () =>
                    {
                        await using var context = new AppDbContext(options);
                        context.PlatformAuditLogs.Add(audit);
                        await context.SaveChangesAsync();
                    },
                    "FOREIGN KEY constraint failed");
            }

            await using var verify = new AppDbContext(options);
            Assert.Equal(0, await verify.PlatformAuditLogs.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AuditLog_RoundTripsWithoutImplicitTenantAndRejectsInvalidValues()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var tenantId = await SeedTenantDataAsync(fixture);
        var platformEvent = NewAudit(null, metadataJson: null);
        platformEvent.Outcome = PlatformAuditOutcome.Denied;
        var tenantEvent = NewAudit(null, metadataJson: """{"reason":"suspended"}""");
        tenantEvent.TargetTenantId = tenantId;
        tenantEvent.TargetType = "Tenant";
        tenantEvent.TargetId = tenantId;
        tenantEvent.RequestId = "req-123";
        await fixture.AddAsync(platformEvent);
        await fixture.AddAsync(tenantEvent);

        await using var context = fixture.CreateContext();
        var storedPlatform = await context.PlatformAuditLogs.AsNoTracking().SingleAsync(item => item.Id == platformEvent.Id);
        var storedTenant = await context.PlatformAuditLogs.AsNoTracking().SingleAsync(item => item.Id == tenantEvent.Id);
        Assert.Null(storedPlatform.ActorPlatformId);
        Assert.Null(storedPlatform.TargetTenantId);
        Assert.Null(storedPlatform.TargetType);
        Assert.Null(storedPlatform.RequestId);
        Assert.Null(storedPlatform.MetadataJson);
        Assert.Equal(PlatformAuditOutcome.Denied, storedPlatform.Outcome);
        Assert.Equal(tenantId, storedTenant.TargetTenantId);
        Assert.Equal("req-123", storedTenant.RequestId);
        Assert.Equal(new[] { "Denied", "Success" }, await fixture.QueryStringsAsync("SELECT outcome FROM platform_audit_logs ORDER BY outcome"));

        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("INSERT INTO platform_audit_logs (id, action, outcome, occurred_at) VALUES ('A0000000-0000-4000-8000-000000000001', 'platform.test', 'Maybe', '2026-10-30')"),
            "CK_platform_audit_logs_outcome");
        await AssertSqliteRejectsAsync(
            () => fixture.ExecuteAsync("INSERT INTO platform_audit_logs (id, action, outcome, occurred_at) VALUES ('A0000000-0000-4000-8000-000000000002', '  ', 'Failed', '2026-10-30')"),
            "CK_platform_audit_logs_action");
    }

    [Fact]
    public async Task Rollback_WithEmptyPlatformTables_RestoresPreviousSchemaAndKeepsTenantData()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync(PreviousMigration);
        await SeedTenantDataAsync(fixture);
        var tenantData = await DumpTenantTablesAsync(fixture);
        var previousObjects = await fixture.QueryStringsAsync("SELECT type || ':' || name FROM sqlite_master ORDER BY 1");
        await using var context = fixture.CreateContext();
        await context.Database.MigrateAsync();

        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        Assert.Equal(previousObjects, await fixture.QueryStringsAsync("SELECT type || ':' || name FROM sqlite_master ORDER BY 1"));
        Assert.Equal(tenantData, await DumpTenantTablesAsync(fixture));
        Assert.DoesNotContain(PlatformMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Rollback_WithPlatformData_IsRefusedAndDestroysNothing()
    {
        await using var fixture = await MigratedSqliteFixture.CreateAsync();
        var admin = await fixture.AddAsync(NewAdmin("ops@example.com"));
        await fixture.AddAsync(NewAudit(admin.Id, metadataJson: null));
        await using var context = fixture.CreateContext();

        await AssertSqliteRejectsAsync(
            () => context.GetService<IMigrator>().MigrateAsync(PreviousMigration),
            "platform_rollback_refused_platform_tables_not_empty");

        Assert.Contains(PlatformMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(PlatformTriggers, await fixture.QueryStringsAsync("SELECT name FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'trg_platform_%' ORDER BY name"));
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_admins"));
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM platform_audit_logs"));
    }

    private static PlatformAdminEntity NewAdmin(string email)
    {
        var now = DateTimeOffset.UtcNow;
        return new PlatformAdminEntity
        {
            Id = Guid.NewGuid(),
            EmailNormalized = email,
            DisplayName = "Platform Operator",
            PasswordHash = ValidPasswordHash,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static PlatformSecurityStateEntity NewState()
    {
        var now = DateTimeOffset.UtcNow;
        return new PlatformSecurityStateEntity
        {
            DeploymentId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static PlatformSecurityStateEntity NewCompletedState(Guid adminId)
    {
        var state = NewState();
        state.BootstrapStatus = PlatformBootstrapStatus.Completed;
        state.BootstrapAdminId = adminId;
        state.BootstrapCompletedAt = state.CreatedAt;
        state.StateVersion = 1;
        return state;
    }

    private static PlatformAuditLogEntity NewAudit(Guid? actorId, string? metadataJson)
    {
        return new PlatformAuditLogEntity
        {
            Id = Guid.NewGuid(),
            ActorPlatformId = actorId,
            Action = "platform.bootstrap.completed",
            Outcome = PlatformAuditOutcome.Success,
            MetadataJson = metadataJson,
            OccurredAt = DateTimeOffset.UtcNow,
        };
    }

    private static async Task<PlatformSecurityStateEntity> CompleteBootstrapAsync(MigratedSqliteFixture fixture, Guid adminId)
    {
        return await fixture.AddAsync(NewCompletedState(adminId));
    }

    private static async Task<Guid> SeedTenantDataAsync(MigratedSqliteFixture fixture)
    {
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var tenant = new TenantEntity { Id = Guid.NewGuid(), Code = "acme", Name = "Acme", CreatedAt = now, UpdatedAt = now };
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "admin@acme.local",
            DisplayName = "Tenant Admin",
            PasswordHash = "tenant-hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await using var context = fixture.CreateContext();
        context.Tenants.Add(tenant);
        context.Users.Add(user);
        context.AuditLogs.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ActorUserId = user.Id,
            Action = "UserCreated",
            EntityType = "User",
            EntityId = user.Id,
            CreatedAt = now,
        });
        await context.SaveChangesAsync();
        return tenant.Id;
    }

    private static async Task<IReadOnlyList<string>> DumpTenantTablesAsync(MigratedSqliteFixture fixture)
    {
        var rows = new List<string>();
        foreach (var table in new[] { "tenants", "users", "audit_logs" })
        {
            rows.Add(await fixture.ScalarTextAsync($"SELECT sql FROM sqlite_master WHERE name = '{table}'"));
            rows.AddRange(await fixture.QueryRowsAsync($"SELECT * FROM \"{table}\" ORDER BY 1"));
        }

        return rows;
    }

    private static void AssertCheckMatchesEnum<TEnum>(IModel model, Type entityType, string constraintName)
        where TEnum : struct, Enum
    {
        var sql = model.FindEntityType(entityType)!.GetCheckConstraints().Single(item => item.Name == constraintName).Sql;
        foreach (var name in Enum.GetNames<TEnum>())
        {
            Assert.Contains($"'{name}'", sql);
        }
    }

    private static async Task AssertSqliteRejectsAsync(Func<Task> action, string expectedMessage)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.NotNull(exception);
        var sqliteException = exception as SqliteException ?? exception.InnerException as SqliteException;
        Assert.True(sqliteException is not null, $"Expected SqliteException but got {exception}");
        Assert.Contains(expectedMessage, sqliteException.Message);
    }

    private sealed class MigratedSqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<AppDbContext> options;

        private MigratedSqliteFixture(SqliteConnection connection)
        {
            this.connection = connection;
            options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        }

        public static async Task<MigratedSqliteFixture> CreateAsync(string? targetMigration = null)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var fixture = new MigratedSqliteFixture(connection);
            await using var context = fixture.CreateContext();
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            return new AppDbContext(options);
        }

        public async Task<TEntity> AddAsync<TEntity>(TEntity entity)
            where TEntity : class
        {
            await using var context = CreateContext();
            context.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> ScalarAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (long)(await command.ExecuteScalarAsync())!;
        }

        public async Task<string> ScalarTextAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (string)(await command.ExecuteScalarAsync())!;
        }

        public async Task<IReadOnlyList<string>> QueryStringsAsync(string sql)
        {
            var values = new List<string>();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetValue(0).ToString()!);
            }

            return values;
        }

        public async Task<IReadOnlyList<string>> QueryRowsAsync(string sql)
        {
            var rows = new List<string>();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "<null>" : reader.GetValue(index).ToString())));
            }

            return rows;
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
