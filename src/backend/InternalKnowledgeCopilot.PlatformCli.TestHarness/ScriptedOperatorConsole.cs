using System.Collections.Concurrent;
using InternalKnowledgeCopilot.PlatformCli;

namespace InternalKnowledgeCopilot.PlatformCli.TestHarness;

// Console giả cho test: trả lời prompt theo thứ tự đã định và ghi lại toàn bộ output để kiểm tra rò rỉ bí mật.
// Không bao giờ dùng trong CLI thật (SystemOperatorConsole từ chối stdin bị chuyển hướng).
public sealed class ScriptedOperatorConsole(IEnumerable<string?> answers, bool interactive = true) : IOperatorConsole
{
    private readonly ConcurrentQueue<string?> answers = new(answers);
    private readonly ConcurrentQueue<string> transcript = new();

    public bool IsInteractive { get; } = interactive;

    public string Output => string.Join(Environment.NewLine, transcript);

    public int RemainingAnswers => answers.Count;

    public void WriteLine(string message) => transcript.Enqueue(message);

    public void WriteError(string message) => transcript.Enqueue(message);

    public string? ReadLine(string prompt)
    {
        transcript.Enqueue(prompt);
        return Next();
    }

    public string? ReadSecret(string prompt)
    {
        transcript.Enqueue(prompt);
        return IsInteractive ? Next() : null;
    }

    private string? Next() => answers.TryDequeue(out var answer) ? answer : null;
}
