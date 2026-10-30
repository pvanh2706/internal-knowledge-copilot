using System.Diagnostics;
using System.Security.Cryptography;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

public sealed record CliRun(int ExitCode, string Output);

// Môi trường S01-C: file SQLite thật đã migrate (trigger S01-A có hiệu lực), authority LocalSeal ngoài thư mục DB
// hoặc DynamoDB giả. Không có tài nguyên AWS nào được tạo hay gọi.
public sealed class S01CTestEnvironment : IAsyncDisposable
{
    public const string AuthorityId = "s01c-test-authority";
    public const string AdminEmail = "first.admin@example.com";
    public const string AdminPassword = "Bootstrap-Secret-Pass-1";
    public const string RecoveryPassword = "Recovery-Secret-Pass-22";
    public const string DynamoTableName = "ikc-platform-provisioning-test";

    private S01CTestEnvironment(string root, bool useDynamoDb)
    {
        Root = root;
        UseDynamoDb = useDynamoDb;
        IntegrityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        LocalSealSettings = new ProvisioningAuthoritySettings(
            ProvisioningAuthorityKind.LocalSeal,
            new ProvisioningAuthorityIdentity(DeploymentId, AuthorityId),
            LocalSealPath: SealPath,
            LocalSealIntegrityKey: Convert.FromBase64String(IntegrityKey));
        DynamoSettings = new ProvisioningAuthoritySettings(
            ProvisioningAuthorityKind.DynamoDb,
            new ProvisioningAuthorityIdentity(DeploymentId, AuthorityId),
            DynamoDbTableName: DynamoTableName,
            DynamoDbRegion: "us-east-1",
            DynamoDbTimeout: TimeSpan.FromSeconds(1));
        Authority = new LocalSealAuthoritySimulator(LocalSealSettings);
    }

    public string Root { get; }

    public bool UseDynamoDb { get; }

    public Guid DeploymentId { get; } = Guid.NewGuid();

    public string IntegrityKey { get; }

    public string DatabasePath => Path.Combine(Root, "data", "platform.db");

    public string SealPath => Path.Combine(Root, "authority", "seal.json");

    public ProvisioningAuthoritySettings LocalSealSettings { get; }

    public ProvisioningAuthoritySettings DynamoSettings { get; }

    public ProvisioningAuthoritySettings Settings => UseDynamoDb ? DynamoSettings : LocalSealSettings;

    public LocalSealAuthoritySimulator Authority { get; }

    public FakeDynamoDbTable Dynamo { get; } = new(DynamoTableName);

    public Func<IProvisioningAuthorityMutator, IProvisioningAuthorityMutator>? MutatorDecorator { get; set; }

    public static async Task<S01CTestEnvironment> CreateAsync(bool useDynamoDb = false, string? targetMigration = null, bool createDatabase = true)
    {
        var environment = new S01CTestEnvironment(Path.Combine(Path.GetTempPath(), $"ikc-s01c-{Guid.NewGuid():N}"), useDynamoDb);
        Directory.CreateDirectory(Path.GetDirectoryName(environment.DatabasePath)!);
        if (createDatabase)
        {
            await using var db = environment.CreateDb();
            await db.GetService<IMigrator>().MigrateAsync(targetMigration);
        }

        return environment;
    }

    public IReadOnlyDictionary<string, string?> Configuration => UseDynamoDb
        ? new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = DatabasePath,
            ["ProvisioningAuthority:Provider"] = "DynamoDb",
            ["ProvisioningAuthority:DeploymentId"] = DeploymentId.ToString(),
            ["ProvisioningAuthority:AuthorityId"] = AuthorityId,
            ["ProvisioningAuthority:DynamoDb:TableName"] = DynamoTableName,
            ["ProvisioningAuthority:DynamoDb:Region"] = "us-east-1",
            ["ProvisioningAuthority:DynamoDb:TimeoutSeconds"] = "1",
        }
        : new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = DatabasePath,
            ["ProvisioningAuthority:Provider"] = "LocalSeal",
            ["ProvisioningAuthority:DeploymentId"] = DeploymentId.ToString(),
            ["ProvisioningAuthority:AuthorityId"] = AuthorityId,
            ["ProvisioningAuthority:LocalSeal:Path"] = SealPath,
            ["ProvisioningAuthority:LocalSeal:IntegrityKey"] = IntegrityKey,
        };

    // Provisioning authority độc lập cấp quyền bootstrap.
    public ProvisioningAuthorityRecord AuthorizeBootstrap()
    {
        var record = new ProvisioningAuthorityRecord
        {
            DeploymentId = DeploymentId,
            AuthorityId = AuthorityId,
            Status = ProvisioningAuthorityStatus.Authorized,
            Version = 1,
            AuthorizationId = $"auth-{Guid.NewGuid():N}",
            AuthorizedAt = AuthorityTimestamp.Now(),
        };
        WriteAuthority(record);
        return record;
    }

    public ProvisioningAuthorityRecord AuthorizeAdminRecovery(Guid target)
    {
        var current = CurrentAuthority()!;
        return WriteAuthority(current with
        {
            Status = ProvisioningAuthorityStatus.RecoveryPending,
            Version = current.Version + 1,
            RecoveryId = Guid.NewGuid(),
            RecoveryPurpose = ProvisioningRecoveryPurpose.AdminRecovery,
            RecoveryTargetAdminId = target,
            RecoveryAuthorizedAt = AuthorityTimestamp.Now(),
        });
    }

    public ProvisioningAuthorityRecord AuthorizeReconciliation()
    {
        var current = CurrentAuthority()!;
        return WriteAuthority(current with
        {
            Status = ProvisioningAuthorityStatus.RecoveryPending,
            Version = current.Version + 1,
            RecoveryId = Guid.NewGuid(),
            RecoveryPurpose = ProvisioningRecoveryPurpose.BootstrapReconciliation,
            RecoveryAuthorizedAt = AuthorityTimestamp.Now(),
        });
    }

    public ProvisioningAuthorityRecord WriteAuthority(ProvisioningAuthorityRecord record)
    {
        if (UseDynamoDb)
        {
            Dynamo.PutRecord(record);
        }
        else
        {
            Authority.Write(record);
        }

        return record;
    }

    public ProvisioningAuthorityRecord? CurrentAuthority() => UseDynamoDb ? Dynamo.Record(DeploymentId) : Authority.Current();

    public IProvisioningAuthorityReader CreateReader() => UseDynamoDb
        ? new DynamoDbProvisioningAuthorityReader(Dynamo.CreateClient("runtime", allowWrites: false), DynamoSettings)
        : new LocalSealProvisioningAuthorityReader(LocalSealSettings);

    public PlatformCliHostOptions HostOptions(IReadOnlyDictionary<string, string?>? extraConfiguration = null, string environmentName = "Testing")
    {
        var configuration = new Dictionary<string, string?>(Configuration);
        foreach (var (key, value) in extraConfiguration ?? new Dictionary<string, string?>())
        {
            configuration[key] = value;
        }

        return new PlatformCliHostOptions
        {
            EnvironmentName = environmentName,
            Configuration = configuration,
            DynamoDbClientFactory = (_, forMutation) => Dynamo.CreateClient(forMutation ? "operator" : "operator-read", allowWrites: forMutation),
            MutatorDecorator = MutatorDecorator,
        };
    }

    public async Task<CliRun> RunCliAsync(string[] args, IEnumerable<string?>? answers = null, bool interactive = true, PlatformCliHostOptions? hostOptions = null)
    {
        var console = new ScriptedOperatorConsole(answers ?? [], interactive);
        var exitCode = await PlatformCliApplication.RunAsync(args, console, hostOptions ?? HostOptions());
        return new CliRun(exitCode, console.Output);
    }

    public Task<CliRun> BootstrapAsync(string email = AdminEmail, string password = AdminPassword)
    {
        return RunCliAsync(["bootstrap", "--email", email, "--display-name", "First Operator"], [password, password, "BOOTSTRAP"]);
    }

    public Task<CliRun> RecoverAdminAsync(string email = AdminEmail, string password = RecoveryPassword)
    {
        return RunCliAsync(["recover-admin", "--email", email], [password, password, "RECOVER"]);
    }

    public Task<CliRun> StatusAsync(string command = "status") => RunCliAsync([command]);

    // Chạy CLI thật trong tiến trình con (harness với câu trả lời qua stdin).
    public async Task<CliRun> RunHarnessProcessAsync(string[] args, IEnumerable<string> answers, TimeSpan timeout)
    {
        var harness = Path.Combine(AppContext.BaseDirectory, "InternalKnowledgeCopilot.PlatformCli.TestHarness.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        start.ArgumentList.Add(harness);
        start.ArgumentList.Add("cli");
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment.Remove("ASPNETCORE_ENVIRONMENT");
        foreach (var (key, value) in Configuration)
        {
            start.Environment[key.Replace(":", "__", StringComparison.Ordinal)] = value;
        }

        using var process = Process.Start(start)!;
        foreach (var answer in answers)
        {
            await process.StandardInput.WriteLineAsync(answer);
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("harness_process_timeout");
        }

        return new CliRun(process.ExitCode, await output + await error);
    }

    public AppDbContext CreateDb()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString();
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var db = CreateDb();
        return await action(db);
    }

    public Task<int> ExecuteSqlAsync(string sql) => WithDbAsync(db => db.Database.ExecuteSqlRawAsync(sql));

    public async Task<long> ScalarAsync(string sql)
    {
        await using var db = CreateDb();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public byte[] SnapshotDatabase()
    {
        SqliteConnection.ClearAllPools();
        return File.ReadAllBytes(DatabasePath);
    }

    public void RestoreDatabase(byte[] snapshot)
    {
        SqliteConnection.ClearAllPools();
        File.WriteAllBytes(DatabasePath, snapshot);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }
}

// Mutator bọc để tiêm lỗi vào đúng chuyển trạng thái (reserve/activate/finalize) của CLI.
public sealed class FaultInjectingMutator(
    IProvisioningAuthorityMutator inner,
    Func<ProvisioningAuthorityRecord, ProvisioningAuthorityRecord, FaultPoint> fault) : IProvisioningAuthorityMutator
{
    public async Task<AuthorityTransitionOutcome> TransitionAsync(ProvisioningAuthorityRecord expected, ProvisioningAuthorityRecord next, CancellationToken cancellationToken = default)
    {
        switch (fault(expected, next))
        {
            case FaultPoint.FailBeforeApply:
                throw new ProvisioningAuthorityUnavailableException("injected_failure");
            case FaultPoint.FailAfterApply:
                await inner.TransitionAsync(expected, next, cancellationToken);
                throw new ProvisioningAuthorityUnavailableException("injected_failure_after_apply");
            case FaultPoint.Crash:
                throw new InvalidOperationException("injected_process_crash");
            default:
                return await inner.TransitionAsync(expected, next, cancellationToken);
        }
    }
}

public enum FaultPoint
{
    None,
    FailBeforeApply,
    FailAfterApply,
    Crash,
}
