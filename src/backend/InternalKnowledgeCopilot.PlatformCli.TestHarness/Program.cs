using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;

// Harness test/dev (IsPublishable=false). Cấu hình đọc giống CLI (appsettings trong --app-dir + biến môi trường).
//   cli <lệnh CLI...>                     chạy CLI thật với câu trả lời prompt đọc từ stdin (mỗi dòng một câu trả lời)
//   cli-pause-after-commit <lệnh CLI...>  như trên nhưng dừng vô hạn ngay sau commit SQLite (test kill process thật, C-IR-10)
//   authority authorize|reconcile|revoke [--app-dir <dir>]
//   authority recover <admin-id> [--app-dir <dir>]
// C-IR-08: không có môi trường mặc định. Chỉ chạy khi DOTNET_ENVIRONMENT được đặt tường minh là Development/Testing,
// ASPNETCORE_ENVIRONMENT (nếu có) trùng với nó, và provider là LocalSeal. Harness không bao giờ chạm Authority Service/DynamoDB.
var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var aspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (environmentName is null
    || !ProvisioningAuthorityConfigurationValidator.IsLocalSealEnvironment(environmentName)
    || (aspNetEnvironment is not null && !string.Equals(aspNetEnvironment, environmentName, StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine("test_harness_restricted_to_development_or_testing");
    return 2;
}

var appDirIndex = Array.IndexOf(args, "--app-dir");
var appDirectory = appDirIndex >= 0 && appDirIndex + 1 < args.Length ? args[appDirIndex + 1] : null;
try
{
    using var probe = PlatformCliContext.Create(appDirectory, new PlatformCliHostOptions { EnvironmentName = environmentName });
    if (probe.Authority is not { Kind: ProvisioningAuthorityKind.LocalSeal })
    {
        Console.Error.WriteLine("test_harness_requires_local_seal");
        return 2;
    }
}
catch (CliConfigurationException)
{
    Console.Error.WriteLine("test_harness_configuration_invalid");
    return CliExitCodes.ConfigurationInvalid;
}

if (args.Length >= 1 && args[0] is HarnessModes.Cli or HarnessModes.CliPauseAfterCommit)
{
    var answers = new List<string?>();
    while (Console.In.ReadLine() is { } line)
    {
        answers.Add(line);
    }

    var hostOptions = new PlatformCliHostOptions
    {
        EnvironmentName = environmentName,

        // Tiến trình test không nâng quyền; chỉ chấp nhận ở đây vì provider đã bị ép là LocalSeal (Development/Testing).
        PrivilegeCheck = () => true,
        AfterSqliteCommit = args[0] == HarnessModes.CliPauseAfterCommit
            ? async () =>
            {
                Console.Out.WriteLine(HarnessModes.PausedMarker);
                Console.Out.Flush();
                await Task.Delay(Timeout.Infinite);
            }
            : null,
    };
    var console = new ScriptedOperatorConsole(answers);
    var exitCode = await PlatformCliApplication.RunAsync(args[1..], console, hostOptions);
    Console.Out.WriteLine(console.Output);
    return exitCode;
}

if (args.Length >= 2 && args[0] == "authority")
{
    using var context = PlatformCliContext.Create(appDirectory, new PlatformCliHostOptions { EnvironmentName = environmentName });
    var simulator = new LocalSealAuthoritySimulator(context.Authority!);
    var record = args[1] switch
    {
        "authorize" => simulator.Authorize(),
        "reconcile" => simulator.AuthorizeBootstrapReconciliation(),
        "recover" when args.Length >= 3 && Guid.TryParse(args[2], out var target) => simulator.AuthorizeAdminRecovery(target),
        "revoke" => simulator.Revoke(),
        _ => null,
    };
    if (record is null)
    {
        Console.Error.WriteLine("unknown_authority_command");
        return 1;
    }

    Console.Out.WriteLine($"{record.Status} version={record.Version} recovery_id={record.RecoveryId?.ToString("D") ?? "-"}");
    return 0;
}

Console.Error.WriteLine("usage: cli|cli-pause-after-commit <command...> | authority authorize|reconcile|revoke|recover <admin-id> [--app-dir <dir>]");
return 1;

namespace InternalKnowledgeCopilot.PlatformCli.TestHarness
{
    public static class HarnessModes
    {
        public const string Cli = "cli";
        public const string CliPauseAfterCommit = "cli-pause-after-commit";
        public const string PausedMarker = "IKC_TEST_HARNESS_PAUSED_AFTER_SQLITE_COMMIT";
    }
}
