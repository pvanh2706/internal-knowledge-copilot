using InternalKnowledgeCopilot.PlatformCli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await PlatformCliApplication.RunAsync(args, new SystemOperatorConsole(), new PlatformCliHostOptions(), cancellation.Token);
