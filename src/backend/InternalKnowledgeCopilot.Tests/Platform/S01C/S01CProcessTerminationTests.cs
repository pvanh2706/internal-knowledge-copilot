using System.Diagnostics;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// C-IR-10: kết thúc tiến trình THẬT (Process.Kill) ngay sau commit SQLite, trước khi authority được activate/finalize. Khác với
// FaultPoint.ThrowException (exception trong cùng tiến trình): ở đây không có finally/catch nào của CLI được chạy.
// Dùng LocalSeal vì TestHarness chỉ chạy với LocalSeal (C-IR-08); điểm dừng là hook AfterSqliteCommit của harness.
public sealed class S01CProcessTerminationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task Bootstrap_ProcessKilledAfterSqliteCommitBeforeActivation_StaysDeniedUntilAuthorizedReconciliation()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();

        var exitCode = await RunUntilPausedThenKillAsync(env,
            ["bootstrap", "--email", S01CTestEnvironment.AdminEmail, "--display-name", "Killed Operator"],
            [S01CTestEnvironment.AdminPassword, S01CTestEnvironment.AdminPassword, "BOOTSTRAP"]);

        Assert.NotEqual(CliExitCodes.Success, exitCode);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.Equal(PlatformBootstrapStatus.Completed, state.BootstrapStatus);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
        Assert.False(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
        Assert.Contains("BootstrapActivationPending", (await env.StatusAsync("recovery-check")).Output);

        env.AuthorizeReconciliation();
        Assert.Equal(CliExitCodes.Success, (await env.RunCliAsync(["recover-admin"], ["RECOVER"])).ExitCode);
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);
    }

    [Fact]
    public async Task AdminRecovery_ProcessKilledAfterSqliteCommitBeforeFinalization_ResumesWithoutReapplying()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);

        var exitCode = await RunUntilPausedThenKillAsync(env,
            ["recover-admin", "--email", S01CTestEnvironment.AdminEmail],
            [S01CTestEnvironment.RecoveryPassword, S01CTestEnvironment.RecoveryPassword, "RECOVER"]);

        Assert.NotEqual(CliExitCodes.Success, exitCode);
        var killed = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.TokenVersion + 1, killed.TokenVersion);
        Assert.Equal(ProvisioningAuthorityStatus.RecoveryPending, env.CurrentAuthority()!.Status);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.False(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
        Assert.Contains("RecoveryAppliedPendingFinalization", (await env.StatusAsync("recovery-check")).Output);

        var resumed = await env.RunCliAsync(["recover-admin"], ["FINALIZE"]);

        Assert.Equal(CliExitCodes.Success, resumed.ExitCode);
        var after = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.TokenVersion + 1, after.TokenVersion);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync(log => log.Action == PlatformAuditActions.AdminRecovered)));
        Assert.Contains("Ready (verified)", (await env.StatusAsync()).Output);
    }

    private static async Task<int> RunUntilPausedThenKillAsync(S01CTestEnvironment env, string[] args, string[] answers)
    {
        using var process = env.StartHarnessProcess(HarnessModes.CliPauseAfterCommit, args);
        var stderr = process.StandardError.ReadToEndAsync();
        foreach (var answer in answers)
        {
            await process.StandardInput.WriteLineAsync(answer);
        }

        process.StandardInput.Close();
        using var cancellation = new CancellationTokenSource(Timeout);
        try
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellation.Token)
                    ?? throw new InvalidOperationException($"harness exited before pausing: {await stderr}");
                if (line == HarnessModes.PausedMarker)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("harness_never_paused");
        }

        Assert.False(process.HasExited);
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
