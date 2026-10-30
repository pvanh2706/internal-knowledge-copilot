namespace InternalKnowledgeCopilot.PlatformCli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;

    // Fail closed, không có thay đổi (hoặc status/recovery-check: trạng thái chưa Ready).
    public const int Denied = 2;
    public const int AuthorityUnavailable = 3;

    // Một kho đã thay đổi nhưng chưa hoàn tất; platform auth vẫn bị từ chối cho tới khi reconciliation được ủy quyền.
    public const int ReconciliationRequired = 4;
    public const int InternalError = 5;
    public const int Cancelled = 6;
    public const int ConfigurationInvalid = 7;
}

public sealed class CliUsageException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

public sealed record CliArguments(string Command, IReadOnlyDictionary<string, string> Options)
{
    public const string Status = "status";
    public const string Bootstrap = "bootstrap";
    public const string RecoveryCheck = "recovery-check";
    public const string RecoverAdmin = "recover-admin";

    private static readonly Dictionary<string, string[]> AllowedOptions = new(StringComparer.Ordinal)
    {
        [Status] = ["app-dir"],
        [RecoveryCheck] = ["app-dir"],
        [Bootstrap] = ["app-dir", "email", "display-name"],
        [RecoverAdmin] = ["app-dir", "email"],
    };

    // Bí mật không bao giờ được truyền qua dòng lệnh (lộ qua lịch sử shell, danh sách process, log).
    private static readonly string[] SecretMarkers = ["password", "passwd", "pwd", "secret", "token", "credential", "key", "hash"];

    public string? Get(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            var name = arg.TrimStart('-').Split('=', 2)[0].ToLowerInvariant();
            if (arg.StartsWith('-') && SecretMarkers.Any(marker => name.Contains(marker, StringComparison.Ordinal)))
            {
                throw new CliUsageException("secret_argument_forbidden");
            }
        }

        if (args.Count == 0 || !AllowedOptions.TryGetValue(args[0], out var allowed))
        {
            throw new CliUsageException("unknown_command");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                // Không nhận tham số vị trí để không thể vô tình truyền mật khẩu như một giá trị tự do.
                throw new CliUsageException("unexpected_argument");
            }

            var parts = token[2..].Split('=', 2);
            var name = parts[0];
            string value;
            if (parts.Length == 2)
            {
                value = parts[1];
            }
            else if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }
            else
            {
                throw new CliUsageException("option_value_missing");
            }

            if (!allowed.Contains(name, StringComparer.Ordinal) || !options.TryAdd(name, value))
            {
                throw new CliUsageException("unknown_or_duplicate_option");
            }
        }

        return new CliArguments(args[0], options);
    }

    public const string Usage = """
        Usage: InternalKnowledgeCopilot.PlatformCli <command> [options]

        Commands (run only by an authorized operator on the deployment host):
          status            Read-only summary of platform bootstrap/provisioning state.
          recovery-check    Read-only diagnosis with reconciliation guidance.
          bootstrap         Create the first Platform Admin (requires an Authorized provisioning record).
          recover-admin     Apply a recovery authorized by the provisioning authority (RecoveryPending).

        Options:
          --app-dir <path>        Directory containing the API appsettings (default: CLI directory).
          --email <email>         bootstrap: first admin email; recover-admin: confirms the target account.
          --display-name <name>   bootstrap: display name of the first admin.

        Mutating commands require an elevated administrator session in every environment. With the DynamoDb provider,
        authority changes go through the Authority Service using the operator's MFA-protected assume-role profile
        (ProvisioningAuthority:DynamoDb:OperatorProfile); the MFA code is requested interactively.
        Passwords are never accepted as arguments; they are requested interactively without echo.
        Exit codes: 0 success/ready, 1 usage, 2 denied/not ready, 3 authority unavailable,
                    4 reconciliation required, 5 internal error, 6 cancelled, 7 configuration invalid.
        """;
}
