namespace InternalKnowledgeCopilot.PlatformCli;

// Entry point có namespace (không dùng top-level statements) để không sinh lớp Program toàn cục trùng với Program của API.
internal static class CliProgram
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        return await PlatformCliApplication.RunAsync(args, new SystemOperatorConsole(), new PlatformCliHostOptions(), cancellation.Token);
    }
}
