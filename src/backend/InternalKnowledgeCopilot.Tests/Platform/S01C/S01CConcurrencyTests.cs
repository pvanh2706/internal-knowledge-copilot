using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// C-G04: nhiều CLI bootstrap/recovery chạy đồng thời trên cùng file SQLite thật không thể tạo hai admin đầu tiên hay
// áp dụng một recovery hai lần.
public sealed class S01CConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentBootstrap_InProcess_CreatesExactlyOneAdmin(bool useDynamoDb)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb);
        env.AuthorizeBootstrap();

        var runs = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            Task.Run(() => env.BootstrapAsync($"admin{index}@example.com"))));

        Assert.Single(runs, run => run.ExitCode == CliExitCodes.Success);
        Assert.All(runs.Where(run => run.ExitCode != CliExitCodes.Success), run => Assert.Equal(CliExitCodes.Denied, run.ExitCode));
        await AssertSingleBootstrapAsync(env);
        Assert.Equal(ProvisioningAuthorityStatus.Activated, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task ConcurrentBootstrap_MultipleOsProcesses_CreatesExactlyOneAdmin()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();

        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(index => env.RunHarnessProcessAsync(
            ["bootstrap", "--email", $"proc{index}@example.com", "--display-name", $"Process {index}"],
            [S01CTestEnvironment.AdminPassword, S01CTestEnvironment.AdminPassword, "BOOTSTRAP"],
            TimeSpan.FromMinutes(3))));

        Assert.True(runs.Count(run => run.ExitCode == CliExitCodes.Success) == 1, string.Join("\n----\n", runs.Select(run => $"{run.ExitCode}: {run.Output}")));
        Assert.All(runs.Where(run => run.ExitCode != CliExitCodes.Success), run => Assert.Equal(CliExitCodes.Denied, run.ExitCode));
        await AssertSingleBootstrapAsync(env);
        Assert.All(runs, run => Assert.DoesNotContain(S01CTestEnvironment.AdminPassword, run.Output));
    }

    // Phòng thủ chiều sâu: kể cả khi authority bị dùng sai (cho mọi process reserve thành công), transaction SQLite
    // BEGIN IMMEDIATE + kiểm tra lại trong transaction + singleton PK vẫn chỉ cho một bootstrap commit.
    [Fact]
    public async Task ConcurrentBootstrap_WithMisbehavingAuthority_SqliteStillCommitsAtMostOneAdmin()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        env.MutatorDecorator = _ => new AlwaysAppliedMutator();

        var runs = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            Task.Run(() => env.BootstrapAsync($"race{index}@example.com"))));

        await AssertSingleBootstrapAsync(env);
        // Authority không được activate thật nên không ai được báo thành công; runtime tiếp tục từ chối.
        Assert.All(runs, run => Assert.Contains(run.ExitCode, new[] { CliExitCodes.ReconciliationRequired, CliExitCodes.Denied }));
        Assert.Contains(runs, run => run.ExitCode == CliExitCodes.ReconciliationRequired);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task ConcurrentRecovery_AppliesExactlyOnceAndRevokesTokensOnce()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        env.AuthorizeAdminRecovery(admin.Id);

        var runs = await Task.WhenAll(Enumerable.Range(0, 5).Select(index =>
            Task.Run(() => env.RecoverAdminAsync(password: $"Concurrent-Recovery-{index}-Pass"))));

        Assert.True(runs.Count(run => run.ExitCode == CliExitCodes.Success) == 1, string.Join("\n----\n", runs.Select(run => run.Output)));
        Assert.All(runs.Where(run => run.ExitCode != CliExitCodes.Success), run => Assert.Equal(CliExitCodes.Denied, run.ExitCode));
        var recovered = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.TokenVersion + 1, recovered.TokenVersion);
        Assert.Equal(2, (await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync())).StateVersion);
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync(log => log.Action == PlatformAuditActions.AdminRecovered)));
        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(1, authority.RecoveryCount);
    }

    private static async Task AssertSingleBootstrapAsync(S01CTestEnvironment env)
    {
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformSecurityState.CountAsync()));
        Assert.Equal(1, await env.WithDbAsync(db => db.PlatformAuditLogs.CountAsync(log => log.Action == PlatformAuditActions.BootstrapCompleted)));
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        var admin = await env.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync());
        Assert.Equal(admin.Id, state.BootstrapAdminId);
    }

    private sealed class AlwaysAppliedMutator : IProvisioningAuthorityMutator
    {
        public Task<AuthorityTransitionOutcome> TransitionAsync(ProvisioningAuthorityRecord expected, ProvisioningAuthorityRecord next, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AuthorityTransitionOutcome.Applied);
        }
    }
}
