using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;

// Harness test/dev. Cấu hình đọc giống CLI (appsettings trong --app-dir + biến môi trường).
//   cli <lệnh CLI...>                 chạy CLI thật với câu trả lời prompt đọc từ stdin (mỗi dòng một câu trả lời)
//   authority authorize|reconcile|revoke [--app-dir <dir>]
//   authority recover <admin-id> [--app-dir <dir>]
var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Testing";
if (!ProvisioningAuthorityConfigurationValidator.IsLocalSealEnvironment(environmentName))
{
    Console.Error.WriteLine("test_harness_restricted_to_development_or_testing");
    return 2;
}

var hostOptions = new PlatformCliHostOptions { EnvironmentName = environmentName };
if (args.Length >= 1 && args[0] == "cli")
{
    var answers = new List<string?>();
    while (Console.In.ReadLine() is { } line)
    {
        answers.Add(line);
    }

    var console = new ScriptedOperatorConsole(answers);
    var exitCode = await PlatformCliApplication.RunAsync(args[1..], console, hostOptions);
    Console.Out.WriteLine(console.Output);
    return exitCode;
}

if (args.Length >= 2 && args[0] == "authority")
{
    var appDirIndex = Array.IndexOf(args, "--app-dir");
    using var context = PlatformCliContext.Create(appDirIndex >= 0 ? args[appDirIndex + 1] : null, hostOptions);
    if (context.Authority is not { Kind: ProvisioningAuthorityKind.LocalSeal } settings)
    {
        Console.Error.WriteLine("local_seal_provider_required");
        return 2;
    }

    var simulator = new LocalSealAuthoritySimulator(settings);
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

Console.Error.WriteLine("usage: cli <command...> | authority authorize|reconcile|revoke|recover <admin-id> [--app-dir <dir>]");
return 1;
