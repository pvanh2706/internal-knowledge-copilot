using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// C-G06 (restore không mở lại bootstrap) và C-G07 (recovery được ủy quyền, có audit, thu hồi token).
public sealed class S01CRestoreRecoveryTests
{
    [Fact]
    public async Task PreBootstrapSqliteRestore_CannotReopenBootstrap()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        var pristine = env.SnapshotDatabase();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);

        env.RestoreDatabase(pristine);
        var retry = await env.BootstrapAsync("attacker@example.com", "Attacker-Chosen-Pass-1");
        var check = await env.StatusAsync("recovery-check");

        Assert.Equal(CliExitCodes.Denied, retry.ExitCode);
        Assert.Contains("authority_already_activated", retry.Output);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Contains("SqliteRestoreSuspected", check.Output);
        Assert.False(ProvisioningEvidence.EvaluateRuntime(null, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
    }

    [Fact]
    public async Task DisablingAllAdminsOrAttemptingDeletion_NeverReopensBootstrap()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);

        await env.ExecuteSqlAsync("UPDATE platform_admins SET status = 'Disabled'");
        await Assert.ThrowsAnyAsync<Exception>(() => env.ExecuteSqlAsync("DELETE FROM platform_admins"));
        await Assert.ThrowsAnyAsync<Exception>(() => env.ExecuteSqlAsync("DELETE FROM platform_security_state"));
        // Authority độc lập cũng không cấp lại được bootstrap khi SQLite đã Completed.
        env.Authority.Authorize();
        var retry = await env.BootstrapAsync("second@example.com");

        Assert.Equal(CliExitCodes.Denied, retry.ExitCode);
        Assert.Contains("bootstrap_already_completed", retry.Output);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    [Fact]
    public async Task CompletedSnapshotOlderThanLastRecovery_IsDetectedByStateVersion()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var beforeRecovery = env.SnapshotDatabase();
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        Assert.Equal(CliExitCodes.Success, (await env.RecoverAdminAsync()).ExitCode);

        // Restore snapshot trước recovery: mật khẩu và token_version cũ sẽ hồi sinh nếu không bị phát hiện.
        env.RestoreDatabase(beforeRecovery);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        var runtime = ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity);

        Assert.False(runtime.Verified);
        Assert.Equal("state_version_mismatch", runtime.Reason);
        Assert.Contains("StateVersionMismatch", (await env.StatusAsync("recovery-check")).Output);
    }

    // GIỚI HẠN ĐÃ BIẾT (C-G09 OPEN): snapshot Completed có cùng state_version (ví dụ trước khi một admin bị vô hiệu hóa hay
    // đổi mật khẩu) KHÔNG bị phát hiện. Test này khẳng định giới hạn để nó không bị báo cáo nhầm là đã được bảo vệ.
    [Fact]
    public async Task CompletedSnapshotWithSameStateVersion_IsNotDetected_DocumentedLimitation()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var snapshot = env.SnapshotDatabase();
        await env.ExecuteSqlAsync("UPDATE platform_admins SET status = 'Disabled', token_version = token_version + 1");

        env.RestoreDatabase(snapshot);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());

        Assert.True(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
        Assert.Equal(PlatformAdminStatus.Active, admin.Status);
        Assert.Contains("C-G09 OPEN", (await env.StatusAsync("recovery-check")).Output);
    }

    [Fact]
    public async Task AdminRecovery_RequiresIndependentAuthorizationAndOperatorConfirmation()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());

        var unauthorized = await env.RecoverAdminAsync();
        Assert.Equal(CliExitCodes.Denied, unauthorized.ExitCode);
        Assert.Contains("recovery_not_authorized", unauthorized.Output);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync(log => log.Action == PlatformAuditActions.RecoveryDenied)));

        env.AuthorizeAdminRecovery(admin.Id);
        var wrongTarget = await env.RecoverAdminAsync(email: "someone.else@example.com");

        Assert.Equal(CliExitCodes.Denied, wrongTarget.ExitCode);
        Assert.Contains("recovery_target_confirmation_mismatch", wrongTarget.Output);
        Assert.Equal(admin.PasswordHash, (await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync())).PasswordHash);
        Assert.Equal(ProvisioningAuthorityStatus.RecoveryPending, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task AdminRecovery_ResetsCredentialRevokesTokensAuditsAndKeepsCompletedStateImmutable()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        await env.ExecuteSqlAsync("UPDATE platform_admins SET status = 'Locked', failed_login_count = 5, locked_until = '2999-01-01 00:00:00+00:00', must_change_password = 0");
        var before = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        var stateBefore = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        var auditBefore = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync());
        var authorization = env.AuthorizeAdminRecovery(before.Id);

        // Trong lúc RecoveryPending, runtime từ chối mọi platform auth.
        Assert.False(ProvisioningEvidence.EvaluateRuntime(stateBefore, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);

        var run = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        var hasher = new PlatformPasswordHasher();
        Assert.True(hasher.VerifyPassword(after.PasswordHash, S01CTestEnvironment.RecoveryPassword));
        Assert.False(hasher.VerifyPassword(after.PasswordHash, S01CTestEnvironment.AdminPassword));
        Assert.Equal(PlatformAdminStatus.Active, after.Status);
        Assert.True(after.MustChangePassword);
        Assert.Equal(0, after.FailedLoginCount);
        Assert.Null(after.LockedUntil);
        Assert.Equal(before.TokenVersion + 1, after.TokenVersion);

        var stateAfter = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.Equal(PlatformBootstrapStatus.Completed, stateAfter.BootstrapStatus);
        Assert.Equal(stateBefore.BootstrapAdminId, stateAfter.BootstrapAdminId);
        Assert.Equal(stateBefore.BootstrapCompletedAt, stateAfter.BootstrapCompletedAt);
        Assert.Equal(stateBefore.StateVersion + 1, stateAfter.StateVersion);

        var auditAfter = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().ToListAsync());
        Assert.All(auditBefore, original => Assert.Contains(auditAfter, row => row.Id == original.Id && row.Action == original.Action && row.MetadataJson == original.MetadataJson));
        var recovered = Assert.Single(auditAfter, row => row.Action == PlatformAuditActions.AdminRecovered);
        Assert.Contains(authorization.RecoveryId!.Value.ToString("D"), recovered.MetadataJson);
        Assert.Equal(after.Id, recovered.TargetId);
        Assert.Null(recovered.ActorPlatformId);
        Assert.Single(auditAfter, row => row.Action == PlatformAuditActions.TokensRevoked);
        Assert.DoesNotContain(S01CTestEnvironment.RecoveryPassword, run.Output);
        Assert.DoesNotContain(after.PasswordHash, run.Output);

        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(authorization.RecoveryId, authority.LastRecoveryId);
        Assert.Equal(1, authority.RecoveryCount);
        Assert.Equal(stateAfter.StateVersion, authority.StateVersion);
        Assert.Null(authority.RecoveryId);
        Assert.True(ProvisioningEvidence.EvaluateRuntime(stateAfter, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);

        // Lặp lại sau khi hoàn tất: không còn ủy quyền.
        var repeat = await env.RecoverAdminAsync(password: "Yet-Another-Strong-Pass-3");
        Assert.Equal(CliExitCodes.Denied, repeat.ExitCode);
        Assert.Contains("recovery_not_authorized", repeat.Output);
    }

    [Fact]
    public async Task InterruptedRecovery_BeforeFinalization_StaysDeniedAndResumesWithoutReapplying()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, step => step == "complete" ? FaultPoint.FailBeforeApply : FaultPoint.None);

        var interrupted = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, interrupted.ExitCode);
        Assert.Equal(ProvisioningAuthorityStatus.RecoveryPending, env.CurrentAuthority()!.Status);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.False(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
        Assert.Contains("RecoveryAppliedPendingFinalization", (await env.StatusAsync("recovery-check")).Output);

        env.TransitionDecorator = null;
        var resumed = await env.RunCliAsync(["recover-admin"], ["FINALIZE"]);

        Assert.Equal(CliExitCodes.Success, resumed.ExitCode);
        Assert.Contains("No credential will be changed", resumed.Output);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.TokenVersion + 1, after.TokenVersion);
        Assert.True(new PlatformPasswordHasher().VerifyPassword(after.PasswordHash, S01CTestEnvironment.RecoveryPassword));
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync(log => log.Action == PlatformAuditActions.AdminRecovered)));
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);
    }

    [Fact]
    public async Task RecoverySqliteFailure_RollsBackAndCanBeRetriedUnderSameAuthorization()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        env.BeforeSqliteCommit = db => db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_block_audit BEFORE INSERT ON platform_audit_logs WHEN NEW.action = 'platform.recovery.admin_recovered' BEGIN SELECT RAISE(ABORT, 'audit_unavailable'); END;");

        var failed = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, failed.ExitCode);
        var unchanged = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.PasswordHash, unchanged.PasswordHash);
        Assert.Equal(admin.TokenVersion, unchanged.TokenVersion);
        Assert.Equal(1, (await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync())).StateVersion);
        Assert.Equal(ProvisioningAuthorityStatus.RecoveryPending, env.CurrentAuthority()!.Status);

        env.BeforeSqliteCommit = null;
        Assert.Equal(CliExitCodes.Success, (await env.RecoverAdminAsync()).ExitCode);
    }

    [Theory]
    [InlineData("unknown_target")]
    [InlineData("state_version")]
    [InlineData("other_deployment")]
    [InlineData("reconcile_wrong_reservation")]
    public async Task Recovery_AuthorityMismatch_FailsClosedWithoutChanges(string mismatch)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        var pending = env.AuthorizeAdminRecovery(admin.Id);
        switch (mismatch)
        {
            case "unknown_target":
                env.WriteAuthority(pending with { RecoveryTargetAdminId = Guid.NewGuid() });
                break;
            case "state_version":
                env.WriteAuthority(pending with { StateVersion = 7 });
                break;
            case "other_deployment":
                env.WriteAuthority(pending with { DeploymentId = Guid.NewGuid() });
                break;
            case "reconcile_wrong_reservation":
                env.WriteAuthority(pending with
                {
                    ReservationId = Guid.NewGuid(),
                    BootstrapAdminId = null,
                    BootstrapCompletedAt = null,
                    ActivatedAt = null,
                    StateVersion = null,
                    RecoveryPurpose = ProvisioningRecoveryPurpose.BootstrapReconciliation,
                    RecoveryTargetAdminId = null,
                });
                break;
        }

        var run = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.PasswordHash, after.PasswordHash);
        Assert.Equal(admin.TokenVersion, after.TokenVersion);
        Assert.DoesNotContain("New password", run.Output);
    }
}
