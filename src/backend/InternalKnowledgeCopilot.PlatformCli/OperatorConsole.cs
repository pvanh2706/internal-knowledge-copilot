using System.Text;

namespace InternalKnowledgeCopilot.PlatformCli;

public interface IOperatorConsole
{
    // Chỉ true khi có terminal tương tác thật; CLI từ chối nhận mật khẩu từ stdin bị chuyển hướng (pipe/file).
    bool IsInteractive { get; }

    void WriteLine(string message);

    void WriteError(string message);

    string? ReadLine(string prompt);

    // Không echo ký tự. Trả null nếu không có terminal tương tác.
    string? ReadSecret(string prompt);
}

public sealed class SystemOperatorConsole : IOperatorConsole
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public void WriteLine(string message) => Console.Out.WriteLine(message);

    public void WriteError(string message) => Console.Error.WriteLine(message);

    public string? ReadLine(string prompt)
    {
        if (!IsInteractive)
        {
            return null;
        }

        Console.Out.Write(prompt);
        return Console.ReadLine();
    }

    public string? ReadSecret(string prompt)
    {
        if (!IsInteractive)
        {
            return null;
        }

        Console.Out.Write(prompt);
        var buffer = new StringBuilder();
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Out.WriteLine();
                    return buffer.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                }
            }
        }
        finally
        {
            buffer.Clear();
        }
    }
}
