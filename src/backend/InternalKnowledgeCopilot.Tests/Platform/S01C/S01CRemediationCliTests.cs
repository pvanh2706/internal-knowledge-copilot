using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.AuthorityService;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// R2/R4/R5: CLI đi qua Authority Service (DynamoDb) với danh tính được xác minh trong audit, MFA, xác nhận đích, toàn vẹn trigger,
// giới hạn TestHarness. Authority Service/API Gateway là bản trong tiến trình; không có AWS thật.
public sealed class S01CRemediationCliTests
{
    // Vector "get-vanilla" của bộ test SigV4 do AWS công bố.
    [Fact]
    public void SigV4Signer_MatchesPublishedAwsTestVector()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.amazonaws.com/");

        SigV4Signer.Sign(request, [], new ImmutableCredentials("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", null), "us-east-1", "service",
            new DateTimeOffset(2015, 8, 30, 12, 36, 0, TimeSpan.Zero));

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/service/aws4_request, SignedHeaders=host;x-amz-date, " +
            "Signature=5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31",
            request.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public void OperatorCredentials_AssumeRoleProfileWithMfa_UsesInteractiveMfaCallback()
    {
        using var profiles = new ProfileFile();
        var console = new ScriptedOperatorConsole(["123456"]);

        var credentials = Assert.IsType<AssumeRoleAWSCredentials>(OperatorCredentialProvider.Create(profiles.Mfa, console, profiles.Path));

        Assert.Equal("arn:aws:iam::111122223333:role/ikc/ikc-operator-alice", credentials.RoleArn);
        Assert.Equal("arn:aws:iam::111122223333:mfa/alice", credentials.Options.MfaSerialNumber);
        Assert.Equal(OperatorCredentialProvider.SessionDurationSeconds, credentials.Options.DurationSeconds);
        Assert.NotNull(credentials.Options.MfaTokenCodeCallback);
        Assert.Equal("123456", credentials.Options.MfaTokenCodeCallback!());
        Assert.Contains("MFA code:", console.Output);
    }

    [Fact]
    public void OperatorCredentials_WithoutMfaOrAssumeRole_AreRejected()
    {
        using var profiles = new ProfileFile();
        var console = new ScriptedOperatorConsole([]);

        Assert.Equal("operator_profile_mfa_required", Assert.Throws<CliConfigurationException>(() => OperatorCredentialProvider.Create(profiles.NoMfa, console, profiles.Path)).Message);
        Assert.Equal("operator_profile_must_assume_role", Assert.Throws<CliConfigurationException>(() => OperatorCredentialProvider.Create(profiles.StaticKeys, console, profiles.Path)).Message);
        Assert.Equal("operator_profile_not_found", Assert.Throws<CliConfigurationException>(() => OperatorCredentialProvider.Create("ikc-missing-profile", console, profiles.Path)).Message);
        var mfa = (AssumeRoleAWSCredentials)OperatorCredentialProvider.Create(profiles.Mfa, new ScriptedOperatorConsole([]), profiles.Path);
        Assert.Throws<InvalidOperationException>(() => mfa.Options.MfaTokenCodeCallback!());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapAndRecoveryAudit_RecordOperatorIdentityAndAssurance(bool useDynamoDb)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        Assert.Equal(CliExitCodes.Success, (await env.RecoverAdminAsync()).ExitCode);

        var audits = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().ToListAsync());
        foreach (var action in new[] { PlatformAuditActions.BootstrapCompleted, PlatformAuditActions.AdminRecovered })
        {
            var metadata = audits.Single(log => log.Action == action).MetadataJson!;
            if (useDynamoDb)
            {
                Assert.Contains("\"operator_person_id\":\"alice\"", metadata);
                Assert.Contains("\"approver_person_id\":\"bob\"", metadata);
                Assert.Contains("\"identity_assurance\":\"authority_service_verified\"", metadata);
            }
            else
            {
                Assert.Contains("\"identity_assurance\":\"local_seal_unverified\"", metadata);
                Assert.Contains("\"operator_person_id\":\"local-seal-operator\"", metadata);
            }
        }

        Assert.Contains("\"operator_person_id\"", audits.Single(log => log.Action == PlatformAuditActions.TokensRevoked).MetadataJson);
    }

    [Fact]
    public async Task MissingVerifiedOperatorIdentity_BlocksSqliteChanges()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new IdentityStrippingClient(inner);

        var bootstrap = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, bootstrap.ExitCode);
        Assert.Contains("operator_identity_unconfirmed", bootstrap.Output);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task MissingVerifiedOperatorIdentityOnRecoveryClaim_DeniesWithoutCredentialChange()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        env.TransitionDecorator = inner => new IdentityStrippingClient(inner);

        var run = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("operator_identity_unconfirmed", run.Output);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.PasswordHash, after.PasswordHash);
        Assert.Equal(admin.TokenVersion, after.TokenVersion);
    }

    [Fact]
    public async Task Recovery_ByNonAssignedOperator_IsDeniedByServiceBeforeAnySqliteChange()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);
        env.CliOperator = env.AuthorityService.SecondOperator;

        var run = await env.RecoverAdminAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("operator_not_assigned", run.Output);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.PasswordHash, after.PasswordHash);
        Assert.Equal(1, (await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync())).StateVersion);
        Assert.Equal(ProvisioningAuthorityStatus.RecoveryPending, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task AdminRecoveryThroughService_RevokesTokensAndConsumesApprovalOnce()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        var pending = env.AuthorizeAdminRecovery(admin.Id);

        var run = await env.RecoverAdminAsync();

        Assert.True(run.ExitCode == CliExitCodes.Success, run.Output);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.TokenVersion + 1, after.TokenVersion);
        Assert.True(new PlatformPasswordHasher().VerifyPassword(after.PasswordHash, S01CTestEnvironment.RecoveryPassword));
        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(2, authority.StateVersion);
        Assert.Equal(pending.RecoveryId, authority.LastRecoveryId);
        Assert.Equal(AuthorityApprovalStatus.Completed, env.AuthorityService.Approval(pending.RecoveryId!.Value).Status);
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);

        var repeat = await env.RecoverAdminAsync(password: "Another-Recovery-Pass-77");
        Assert.Equal(CliExitCodes.Denied, repeat.ExitCode);
        Assert.Contains("recovery_not_authorized", repeat.Output);
    }

    // SQLite đã commit nhưng activation thất bại: lệch trạng thái fail closed cho tới reconciliation được approver độc lập cấp.
    [Fact]
    public async Task SqliteCommittedButActivationFailed_IsReconciledOnlyThroughIndependentApproval()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        env.TransitionDecorator = inner => new FaultInjectingTransitionClient(inner, step => step == "activate" ? FaultPoint.FailBeforeApply : FaultPoint.None);

        Assert.Equal(CliExitCodes.ReconciliationRequired, (await env.BootstrapAsync()).ExitCode);
        env.TransitionDecorator = null;
        Assert.Contains("BootstrapActivationPending", (await env.StatusAsync("recovery-check")).Output);
        Assert.Contains("recovery_not_authorized", (await env.RunCliAsync(["recover-admin"], ["RECOVER"])).Output);

        env.AuthorizeReconciliation();
        var reconcile = await env.RunCliAsync(["recover-admin"], ["RECOVER"]);

        Assert.True(reconcile.ExitCode == CliExitCodes.Success, reconcile.Output);
        var authority = env.CurrentAuthority()!;
        Assert.Equal(2, authority.StateVersion);
        Assert.Equal(1, authority.RecoveryCount);
        var reconciled = await env.WithDbAsync(db => db.PlatformAuditLogs.AsNoTracking().SingleAsync(log => log.Action == PlatformAuditActions.BootstrapReconciled));
        Assert.Contains("\"operator_person_id\":\"alice\"", reconciled.MetadataJson);
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);
    }

    [Fact]
    public async Task LostServiceResponse_IsRetriedWithSameRequestIdAndNotReapplied()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var dropped = 0;
        env.AuthorityService.DropResponseAfterHandling = route => route == AuthorityServiceRoutes.Reserve && Interlocked.Increment(ref dropped) == 1;

        var run = await env.BootstrapAsync();

        Assert.True(run.ExitCode == CliExitCodes.Success, run.Output);
        var reserveEvents = env.AuthorityService.Audit.Events.Where(item => item.Route == AuthorityServiceRoutes.Reserve).ToList();
        Assert.Single(reserveEvents, item => item.EventType == AuthorityAuditEventTypes.TransitionApplied);
        Assert.Single(reserveEvents, item => item.EventType == AuthorityAuditEventTypes.RequestReplayed);
        Assert.Equal(3, env.CurrentAuthority()!.Version);
    }

    [Fact]
    public async Task ServiceUnreachable_BeforeReserve_ChangesNothing()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        var authorized = env.AuthorizeBootstrap();
        env.AuthorityService.FailBeforeHandling = route => route == AuthorityServiceRoutes.Reserve;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.AuthorityUnavailable, run.ExitCode);
        Assert.Contains("authority_service_unreachable", run.Output);
        Assert.Equal(authorized, env.CurrentAuthority());
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    // C-IR-07: CLI không hiển thị email đích; operator phải nhập email từ yêu cầu recovery đã duyệt.
    [Fact]
    public async Task RecoveryTargetConfirmation_DoesNotRevealTheExpectedEmail()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);

        var run = await env.RunCliAsync(["recover-admin"], [S01CTestEnvironment.AdminEmail, S01CTestEnvironment.RecoveryPassword, S01CTestEnvironment.RecoveryPassword, "RECOVER"]);

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        Assert.Contains("Target account email from the approved recovery request", run.Output);
        Assert.Contains(admin.Id.ToString("D"), run.Output);
        Assert.DoesNotContain(S01CTestEnvironment.AdminEmail, run.Output);
    }

    public static TheoryData<string> TriggerTampering => new()
    {
        // Thay bằng bản no-op cùng tên.
        "DROP TRIGGER trg_platform_admins_no_delete; CREATE TRIGGER \"trg_platform_admins_no_delete\" BEFORE DELETE ON \"platform_admins\" BEGIN SELECT 1; END;",
        "DROP TRIGGER trg_platform_security_state_guard_update; CREATE TRIGGER \"trg_platform_security_state_guard_update\" BEFORE UPDATE ON \"platform_security_state\" BEGIN SELECT 1; END;",

        // Trigger lạ trên bảng platform.
        "CREATE TRIGGER helper_audit_rewrite AFTER INSERT ON platform_audit_logs BEGIN SELECT 1; END;",
    };

    [Theory]
    [MemberData(nameof(TriggerTampering))]
    public async Task Preflight_DetectsTamperedOrForeignPlatformTriggers(string sql)
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        await env.ExecuteSqlAsync(sql);

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("schema_integrity_failed", run.Output);
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    public static TheoryData<string?, string?, bool, string> HarnessEnvironments => new()
    {
        { null, null, false, "test_harness_restricted_to_development_or_testing" },
        { "Production", null, false, "test_harness_restricted_to_development_or_testing" },
        { "Testing", "Production", false, "test_harness_restricted_to_development_or_testing" },
        { "Testing", null, true, "test_harness_requires_local_seal" },
    };

    // C-IR-08: harness không có môi trường mặc định và chỉ chạy với LocalSeal.
    [Theory]
    [MemberData(nameof(HarnessEnvironments))]
    public async Task TestHarness_RefusesOutsideExplicitTestingWithLocalSeal(string? dotnetEnvironment, string? aspNetEnvironment, bool useDynamoDb, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb);

        var run = await env.RunHarnessProcessAsync(["status"], [], TimeSpan.FromMinutes(2), HarnessModes.Cli, new Dictionary<string, string?>
        {
            ["DOTNET_ENVIRONMENT"] = dotnetEnvironment,
            ["ASPNETCORE_ENVIRONMENT"] = aspNetEnvironment,
        });

        Assert.Equal(2, run.ExitCode);
        Assert.Contains(reason, run.Output);
    }

    private sealed class IdentityStrippingClient(IAuthorityTransitionClient inner) : IAuthorityTransitionClient
    {
        public async Task<AuthorityStepResult> ReserveAsync(ProvisioningAuthorityRecord authorized, Guid reservedAdminId, string requestId, CancellationToken cancellationToken = default)
            => (await inner.ReserveAsync(authorized, reservedAdminId, requestId, cancellationToken)) with { Operator = null };

        public Task<AuthorityStepResult> ActivateAsync(ProvisioningAuthorityRecord reserved, DateTimeOffset bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default)
            => inner.ActivateAsync(reserved, bootstrapCompletedAt, requestId, cancellationToken);

        public async Task<AuthorityStepResult> ClaimRecoveryAsync(ProvisioningAuthorityRecord pending, string requestId, CancellationToken cancellationToken = default)
            => (await inner.ClaimRecoveryAsync(pending, requestId, cancellationToken)) with { Operator = null };

        public Task<AuthorityStepResult> CompleteRecoveryAsync(ProvisioningAuthorityRecord pending, int stateVersion, DateTimeOffset? bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default)
            => inner.CompleteRecoveryAsync(pending, stateVersion, bootstrapCompletedAt, requestId, cancellationToken);
    }

    private sealed class ProfileFile : IDisposable
    {
        private readonly string suffix = Guid.NewGuid().ToString("N")[..8];

        public ProfileFile()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ikc-aws-profiles-{suffix}.ini");
            File.WriteAllText(Path, $"""
                [{Base}]
                aws_access_key_id = AKIABASEEXAMPLE0001
                aws_secret_access_key = base-secret-example

                [{Mfa}]
                role_arn = arn:aws:iam::111122223333:role/ikc/ikc-operator-alice
                source_profile = {Base}
                mfa_serial = arn:aws:iam::111122223333:mfa/alice

                [{NoMfa}]
                role_arn = arn:aws:iam::111122223333:role/ikc/ikc-operator-alice
                source_profile = {Base}

                [{StaticKeys}]
                aws_access_key_id = AKIASTATICEXAMPLE01
                aws_secret_access_key = static-secret-example

                """);
        }

        public string Path { get; }

        public string Base => $"ikc-base-{suffix}";

        public string Mfa => $"ikc-mfa-{suffix}";

        public string NoMfa => $"ikc-nomfa-{suffix}";

        public string StaticKeys => $"ikc-static-{suffix}";

        public void Dispose() => File.Delete(Path);
    }
}
