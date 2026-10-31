using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.Bootstrap;
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

    // Authority hoạt động sai (báo Applied nhưng không đổi trạng thái): consistent read sau reserve không xác nhận reservation
    // của chính process này nên CLI không bao giờ ghi SQLite và không báo thành công.
    [Fact]
    public async Task Bootstrap_AuthorityReportsAppliedWithoutStateChange_NeverWritesSqlite()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        env.AuthorizeBootstrap();
        env.TransitionDecorator = _ => new AlwaysAppliedTransitionClient();

        var runs = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            Task.Run(() => env.BootstrapAsync($"race{index}@example.com"))));

        Assert.All(runs, run =>
        {
            Assert.Equal(CliExitCodes.ReconciliationRequired, run.ExitCode);
            Assert.Contains("authority_reservation_not_confirmed", run.Output);
        });
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformSecurityState.CountAsync()));
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, env.CurrentAuthority()!.Status);
    }

    // Phòng thủ chiều sâu ở tầng SQLite: kể cả khi nhiều process cùng có "reservation" (authority bị thao túng), transaction
    // BEGIN IMMEDIATE + kiểm tra lại trong transaction + singleton PK chỉ cho đúng một bootstrap commit.
    [Fact]
    public async Task ConcurrentSqliteBootstrapCommits_WithForgedReservations_CommitExactlyOneAdmin()
    {
        await using var env = await S01CTestEnvironment.CreateAsync();
        var authorized = env.AuthorizeBootstrap();
        using var context = PlatformCliContext.Create(null, env.HostOptions());
        var service = new PlatformBootstrapService(context, new AlwaysAppliedTransitionClient());
        var passwordHash = new PlatformPasswordHasher().HashPassword(S01CTestEnvironment.AdminPassword);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => Task.Run(() => service.CommitSqliteAsync(
            new BootstrapInput($"forged{index}@example.com", $"Forged {index}", passwordHash),
            context.Authority!,
            authorized with
            {
                Status = ProvisioningAuthorityStatus.Reserved,
                Version = authorized.Version + 1,
                ReservationId = Guid.NewGuid(),
                ReservedAdminId = Guid.NewGuid(),
                ReservedAt = AuthorityTimestamp.Now(),
            },
            AuthorityTimestamp.Now(),
            OperatorIdentity.UnverifiedLocal(),
            $"forged-{index}"))));

        Assert.Single(results, result => result is null);
        Assert.All(results.Where(result => result is not null), result => Assert.Equal(OperationOutcome.ReconciliationRequired, result!.Outcome));
        await AssertSingleBootstrapAsync(env);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRecovery_AppliesExactlyOnceAndRevokesTokensOnce(bool useDynamoDb)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb);
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

    private sealed class AlwaysAppliedTransitionClient : IAuthorityTransitionClient
    {
        private static Task<AuthorityStepResult> Applied() => Task.FromResult(AuthorityStepResult.Applied("forged", OperatorIdentity.UnverifiedLocal()));

        public Task<AuthorityStepResult> ReserveAsync(ProvisioningAuthorityRecord authorized, Guid reservedAdminId, string requestId, CancellationToken cancellationToken = default) => Applied();

        public Task<AuthorityStepResult> ActivateAsync(ProvisioningAuthorityRecord reserved, DateTimeOffset bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default) => Applied();

        public Task<AuthorityStepResult> ClaimRecoveryAsync(ProvisioningAuthorityRecord pending, string requestId, CancellationToken cancellationToken = default) => Applied();

        public Task<AuthorityStepResult> CompleteRecoveryAsync(ProvisioningAuthorityRecord pending, int stateVersion, DateTimeOffset? bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default) => Applied();
    }
}
