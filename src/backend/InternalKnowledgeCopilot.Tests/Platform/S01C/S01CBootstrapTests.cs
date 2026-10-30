using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// S01-C C1/C2/C4: bootstrap qua CLI thật (composition thật, console scripted) trên SQLite thật.
public sealed class S01CBootstrapTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bootstrap_ValidFirstRun_CreatesExactlyOneAdminCompletedStateAndAuditAndActivatesAuthority(bool useDynamoDb)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb);
        env.AuthorizeBootstrap();

        var run = await env.BootstrapAsync();

        Assert.True(run.ExitCode == CliExitCodes.Success, run.Output);
        Assert.Contains("result: Success (bootstrap_completed)", run.Output);
        var admin = Assert.Single(await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().ToListAsync()));
        Assert.Equal(S01CTestEnvironment.AdminEmail, admin.EmailNormalized);
        Assert.Equal("First Operator", admin.DisplayName);
        Assert.Equal(PlatformAdminStatus.Active, admin.Status);
        Assert.True(admin.MustChangePassword);
        Assert.Equal(0, admin.TokenVersion);
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", admin.PasswordHash);
        Assert.True(new PlatformPasswordHasher().VerifyPassword(admin.PasswordHash, S01CTestEnvironment.AdminPassword));

        var state = Assert.Single(await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().ToListAsync()));
        Assert.Equal(PlatformBootstrapStatus.Completed, state.BootstrapStatus);
        Assert.Equal(admin.Id, state.BootstrapAdminId);
        Assert.Equal(env.DeploymentId, state.DeploymentId);
        Assert.Equal(1, state.StateVersion);

        var audit = Assert.Single(await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().ToListAsync()));
        Assert.Equal(PlatformAuditActions.BootstrapCompleted, audit.Action);
        Assert.Equal(PlatformAuditOutcome.Success, audit.Outcome);
        Assert.Null(audit.ActorPlatformId);
        Assert.Null(audit.TargetTenantId);
        Assert.Equal(admin.Id, audit.TargetId);

        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(3, authority.Version);
        Assert.Equal(admin.Id, authority.BootstrapAdminId);
        Assert.Equal(1, authority.StateVersion);
        Assert.True(AuthorityTimestamp.AreEqual(state.BootstrapCompletedAt, authority.BootstrapCompletedAt));
        Assert.Contains(authority.ReservationId!.Value.ToString("D"), audit.MetadataJson);

        var verifier = new ProvisioningAuthorityVerifier(env.CreateReader(), env.Settings, NullLogger<ProvisioningAuthorityVerifier>.Instance);
        Assert.True(await verifier.IsProvisioningVerifiedAsync(state));
        var status = await env.StatusAsync();
        Assert.Equal(CliExitCodes.Success, status.ExitCode);
        Assert.Contains("Ready (verified)", status.Output);
    }

    [Fact]
    public async Task Bootstrap_Repeated_IsDeniedWithoutChangingAdminStateOrAuthority()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var authorityBefore = env.CurrentAuthority();

        var second = await env.BootstrapAsync("second.admin@example.com", "Another-Strong-Pass-9");

        Assert.Equal(CliExitCodes.Denied, second.ExitCode);
        Assert.Contains("bootstrap_already_completed", second.Output);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(1, (await env.WithDbAsync(db => db.PlatformSecurityState.SingleAsync())).StateVersion);
        Assert.Equal(authorityBefore, env.CurrentAuthority());
        // Từ chối sau bootstrap được audit; không có prompt mật khẩu nào được hiển thị.
        var denial = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().SingleAsync(log => log.Action == PlatformAuditActions.BootstrapDenied));
        Assert.Equal(PlatformAuditOutcome.Denied, denial.Outcome);
        Assert.DoesNotContain("New password", second.Output);
    }

    [Fact]
    public async Task Bootstrap_FailureBeforeSqliteCommit_MandatoryAuditFails_RollsBackEverythingAndReservationDoesNotExpire()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        // Audit bắt buộc không ghi được → toàn bộ transaction SQLite phải rollback. Trigger chặn được tạo BÊN TRONG transaction
        // (sau preflight), vì preflight từ chối mọi trigger lạ trên bảng platform (C-IR-09).
        env.BeforeSqliteCommit = db => db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_block_audit BEFORE INSERT ON platform_audit_logs BEGIN SELECT RAISE(ABORT, 'audit_unavailable'); END;");

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, run.ExitCode);
        Assert.Contains("sqlite_transaction_failed", run.Output);
        Assert.Contains("NOT complete", run.Output);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformSecurityState.CountAsync()));
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync()));
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);

        var check = await env.StatusAsync("recovery-check");
        Assert.Equal(CliExitCodes.Denied, check.ExitCode);
        Assert.Contains("ReservationWithoutSqliteEvidence", check.Output);

        // Reservation không tự hết hạn thành cơ hội bootstrap mới, kể cả khi lỗi đã được khắc phục.
        env.BeforeSqliteCommit = null;
        Assert.Equal(0L, await env.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'test_block_audit'"));
        var retry = await env.BootstrapAsync();
        Assert.Equal(CliExitCodes.Denied, retry.ExitCode);
        Assert.Contains("authority_reservation_pending", retry.Output);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));

        // Chỉ quyết định tường minh của provisioning authority độc lập mới mở lại.
        env.Authority.Authorize();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
    }

    [Fact]
    public async Task Bootstrap_FailureAfterSqliteCommitBeforeActivation_DeniesAuthUntilAuthorizedReconciliation()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, step => step == "activate" ? FaultPoint.FailBeforeApply : FaultPoint.None);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, run.ExitCode);
        Assert.Contains("injected_failure", run.Output);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.Equal(PlatformBootstrapStatus.Completed, state.BootstrapStatus);
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
        var runtime = ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity);
        Assert.False(runtime.Verified);
        Assert.Equal("authority_activation_pending", runtime.Reason);
        Assert.Contains("BootstrapActivationPending", (await env.StatusAsync("recovery-check")).Output);

        // recover-admin không có RecoveryPending độc lập thì bị từ chối.
        env.TransitionDecorator = null;
        var unauthorized = await env.RunCliAsync(["recover-admin"], ["RECOVER"]);
        Assert.Equal(CliExitCodes.Denied, unauthorized.ExitCode);
        Assert.Contains("recovery_not_authorized", unauthorized.Output);

        env.AuthorizeReconciliation();
        var reconcile = await env.RunCliAsync(["recover-admin"], ["RECOVER"]);

        Assert.Equal(CliExitCodes.Success, reconcile.ExitCode);
        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(2, authority.StateVersion);
        Assert.Equal(1, authority.RecoveryCount);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(1, admin.TokenVersion);
        Assert.True(admin.MustChangePassword);
        Assert.True(new PlatformPasswordHasher().VerifyPassword(admin.PasswordHash, S01CTestEnvironment.AdminPassword));
        var actions = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().Select(log => log.Action).ToListAsync());
        Assert.Contains(PlatformAuditActions.BootstrapCompleted, actions);
        Assert.Contains(PlatformAuditActions.BootstrapReconciled, actions);
        Assert.Contains(PlatformAuditActions.TokensRevoked, actions);
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);
    }

    [Fact]
    public async Task Bootstrap_ActivationAppliedButResponseLost_IsVerifiedByStronglyConsistentReread()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, step => step == "activate" ? FaultPoint.FailAfterApply : FaultPoint.None);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        Assert.Equal(ProvisioningAuthorityStatus.Activated, env.CurrentAuthority()!.Status);
    }

    [Fact]
    // C-IR-10: đây là exception tiêm trong cùng tiến trình, KHÔNG phải process bị kill (xem S01CProcessTerminationTests).
    public async Task Bootstrap_ExceptionInjectedBetweenCommitAndActivation_NeverReportsSuccessAndStaysDenied()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, step => step == "activate" ? FaultPoint.ThrowException : FaultPoint.None);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.InternalError, run.ExitCode);
        Assert.DoesNotContain("Success", run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
        Assert.Contains("BootstrapActivationPending", (await env.StatusAsync("recovery-check")).Output);
    }

    [Fact]
    public async Task Bootstrap_ReservationUnavailable_ChangesNothing()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        var authorized = env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, _ => FaultPoint.FailBeforeApply);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.AuthorityUnavailable, run.ExitCode);
        Assert.Equal(authorized, env.CurrentAuthority());
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    public static TheoryData<string> AuthorityFaults => new()
    {
        "missing", "revoked", "other_deployment", "other_authority", "tampered", "already_activated", "reserved", "recovery_pending",
    };

    [Theory]
    [MemberData(nameof(AuthorityFaults))]
    public async Task Bootstrap_MissingRevokedMismatchedOrTamperedAuthority_IsDeniedWithoutChanges(string fault)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        if (fault != "missing")
        {
            var authorized = env.AuthorizeBootstrap();
            switch (fault)
            {
                case "revoked":
                    env.Authority.Revoke();
                    break;
                case "other_deployment":
                    env.Authority.Write(authorized with { DeploymentId = Guid.NewGuid() });
                    break;
                case "other_authority":
                    env.Authority.Write(authorized with { AuthorityId = "someone-else" });
                    break;
                case "tampered":
                    var text = await File.ReadAllTextAsync(env.SealPath);
                    await File.WriteAllTextAsync(env.SealPath, text.Replace("\"mac\":\"", "\"mac\":\"AAAA", StringComparison.Ordinal));
                    break;
                case "already_activated":
                case "reserved":
                case "recovery_pending":
                    var reserved = env.Authority.Write(authorized with
                    {
                        Status = ProvisioningAuthorityStatus.Reserved,
                        ReservationId = Guid.NewGuid(),
                        ReservedAdminId = Guid.NewGuid(),
                        ReservedAt = AuthorityTimestamp.Now(),
                    });
                    if (fault == "recovery_pending")
                    {
                        env.Authority.AuthorizeBootstrapReconciliation();
                    }
                    else if (fault == "already_activated")
                    {
                        env.Authority.Write(reserved with
                        {
                            Status = ProvisioningAuthorityStatus.Activated,
                            BootstrapAdminId = reserved.ReservedAdminId,
                            BootstrapCompletedAt = AuthorityTimestamp.Now(),
                            ActivatedAt = AuthorityTimestamp.Now(),
                            StateVersion = 1,
                        });
                    }

                    break;
            }
        }

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformSecurityState.CountAsync()));
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync()));
        Assert.DoesNotContain("New password", run.Output);
    }

    [Theory]
    [InlineData("INSERT INTO platform_admins (id, email_normalized, display_name, password_hash, status, failed_login_count, must_change_password, token_version, created_at, updated_at) VALUES ('6f1e0b1c-0000-4000-8000-000000000001', 'x@example.com', 'X', '$argon2id$v=19$m=65536,t=3,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g', 'Disabled', 0, 1, 0, '2026-01-01', '2026-01-01')", "platform_admin_exists")]
    [InlineData("INSERT INTO platform_audit_logs (id, action, outcome, occurred_at) VALUES ('6f1e0b1c-0000-4000-8000-000000000002', 'platform.auth.login_failed', 'Failed', '2026-01-01')", "platform_audit_history_present")]
    [InlineData("INSERT INTO platform_security_state (id, deployment_id, bootstrap_status, state_version, created_at, updated_at) VALUES (1, '6f1e0b1c-0000-4000-8000-000000000003', 'NotStarted', 0, '2026-01-01', '2026-01-01')", "security_state_incompatible")]
    [InlineData("DROP TRIGGER trg_platform_security_state_guard_update", "schema_integrity_failed")]
    public async Task Bootstrap_AnomalousPreBootstrapState_FailsClosed(string setupSql, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        await env.ExecuteSqlAsync(setupSql);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains(reason, run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, env.CurrentAuthority()!.Status);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformSecurityState.CountAsync(state => state.BootstrapStatus == PlatformBootstrapStatus.Completed)));
    }

    [Fact]
    public async Task Bootstrap_PendingMigrations_IsDeniedAndCliNeverMigrates()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(targetMigration: "20260605010352_ProductHardeningPhase10");
        env.AuthorizeBootstrap();
        var before = env.SnapshotDatabase();

        var run = await env.BootstrapAsync();
        var status = await env.StatusAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("schema_not_current", run.Output);
        Assert.Contains("schema_not_current", status.Output);
        Assert.Equal(before, env.SnapshotDatabase());
        Assert.Equal(0L, await env.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'platform_admins'"));
    }

    [Fact]
    public async Task Bootstrap_MissingDatabase_IsDeniedAndDatabaseIsNotCreated()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(createDatabase: false);
        env.AuthorizeBootstrap();

        var run = await env.BootstrapAsync();
        var status = await env.StatusAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("database_not_found", run.Output);
        Assert.Contains("DatabaseUnavailable", status.Output);
        Assert.False(File.Exists(env.DatabasePath));
    }

    [Theory]
    [InlineData("--password=Bootstrap-Secret-Pass-1")]
    [InlineData("--admin-password")]
    [InlineData("--token")]
    [InlineData("--integrity-key")]
    public async Task Cli_SecretOnCommandLine_IsRejectedWithoutEchoingIt(string argument)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();

        var run = await env.RunCliAsync(["bootstrap", argument, "Bootstrap-Secret-Pass-1"]);

        Assert.Equal(CliExitCodes.UsageError, run.ExitCode);
        Assert.Contains("secret_argument_forbidden", run.Output);
        Assert.DoesNotContain("Bootstrap-Secret-Pass-1", run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task Cli_PositionalArgument_IsRejected()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();

        var run = await env.RunCliAsync(["bootstrap", "Bootstrap-Secret-Pass-1"]);

        Assert.Equal(CliExitCodes.UsageError, run.ExitCode);
        Assert.DoesNotContain("Bootstrap-Secret-Pass-1", run.Output);
    }

    [Fact]
    public async Task Bootstrap_NonInteractiveInput_IsRefusedBeforeAnyMutation()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        var authorized = env.AuthorizeBootstrap();

        var run = await env.RunCliAsync(["bootstrap", "--email", S01CTestEnvironment.AdminEmail, "--display-name", "Ops"],
            [S01CTestEnvironment.AdminPassword, S01CTestEnvironment.AdminPassword, "BOOTSTRAP"], interactive: false);

        Assert.Equal(CliExitCodes.UsageError, run.ExitCode);
        Assert.Contains("interactive_terminal_required", run.Output);
        Assert.Equal(authorized, env.CurrentAuthority());
    }

    [Theory]
    [InlineData("Bootstrap-Secret-Pass-1", "Bootstrap-Secret-Pass-2", "BOOTSTRAP", CliExitCodes.UsageError, "password_confirmation_mismatch")]
    [InlineData("short", "short", "BOOTSTRAP", CliExitCodes.UsageError, "password_policy")]
    [InlineData("Bootstrap-Secret-Pass-1", "Bootstrap-Secret-Pass-1", "no", CliExitCodes.Cancelled, "cancelled")]
    public async Task Bootstrap_InvalidOperatorInput_ChangesNothing(string password, string confirmation, string confirm, int exitCode, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        var authorized = env.AuthorizeBootstrap();

        var run = await env.RunCliAsync(["bootstrap", "--email", S01CTestEnvironment.AdminEmail, "--display-name", "Ops"], [password, confirmation, confirm]);

        Assert.Equal(exitCode, run.ExitCode);
        Assert.Contains(reason, run.Output);
        Assert.DoesNotContain(password, run.Output);
        Assert.Equal(authorized, env.CurrentAuthority());
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    [Fact]
    public async Task Bootstrap_OutputsAndAudit_NeverContainPasswordHashOrSecrets()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();

        var run = await env.BootstrapAsync();
        var status = await env.StatusAsync("recovery-check");

        var hash = (await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync())).PasswordHash;
        var audits = string.Join("\n", await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().Select(log => log.MetadataJson ?? string.Empty).ToListAsync()));
        foreach (var text in new[] { run.Output, status.Output, audits })
        {
            Assert.DoesNotContain(S01CTestEnvironment.AdminPassword, text);
            Assert.DoesNotContain(hash, text);
            Assert.DoesNotContain("$argon2id", text);
            Assert.DoesNotContain(env.IntegrityKey, text);
        }

        Assert.DoesNotContain(S01CTestEnvironment.AdminEmail, audits);
    }

    [Fact]
    public async Task ReadOnlyCommands_DoNotMutateDatabaseOrAuthority()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var database = env.SnapshotDatabase();
        var seal = await File.ReadAllBytesAsync(env.SealPath);

        foreach (var command in new[] { "status", "recovery-check", "status", "recovery-check" })
        {
            Assert.Equal(CliExitCodes.Success, (await env.StatusAsync(command)).ExitCode);
        }

        Assert.Equal(database, env.SnapshotDatabase());
        Assert.Equal(seal, await File.ReadAllBytesAsync(env.SealPath));
    }

    [Fact]
    public async Task Cli_LocalSealInProduction_IsRejectedAsConfigurationError()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: env.HostOptions(environmentName: "Production"));
        var status = await env.RunCliAsync(["status"], hostOptions: env.HostOptions(environmentName: "Production"));

        Assert.Equal(CliExitCodes.ConfigurationInvalid, run.ExitCode);
        Assert.Equal(CliExitCodes.ConfigurationInvalid, status.ExitCode);
        Assert.Contains("LocalSeal", run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task Cli_ProviderNone_DeniesMutations()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: env.HostOptions(new Dictionary<string, string?> { ["ProvisioningAuthority:Provider"] = "None" }));

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("provisioning_authority_not_configured", run.Output);
    }
}
