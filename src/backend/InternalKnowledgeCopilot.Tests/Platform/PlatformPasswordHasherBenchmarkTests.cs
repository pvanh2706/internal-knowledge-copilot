using System.Diagnostics;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Xunit.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Platform;

// Test nhạy thời gian chạy một mình: xUnit chạy collection này sau và không song song với mọi collection khác.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingSensitiveTestCollection
{
    public const string Name = "timing-sensitive";
}

// S01-B: SimulateVerification (đường "không có tài khoản") phải tốn thời gian tương đương VerifyPassword để không lộ tài khoản
// qua thời gian phản hồi. C-IR-06: trước đây đo ba chuỗi liên tiếp trong khi các test S01-C (Argon2, đa tiến trình) chạy song song
// nên tỷ lệ dao động. Nay chạy tuần tự (collection riêng) và đo xen kẽ theo cặp để tải hệ thống tác động đều lên cả hai phép đo.
[Collection(TimingSensitiveTestCollection.Name)]
public sealed class PlatformPasswordHasherBenchmarkTests(ITestOutputHelper output)
{
    private const string Password = "Correct-Horse-Battery-9";
    private const int Rounds = 7;

    [Fact]
    public void PasswordHasher_Benchmark()
    {
        var hasher = new PlatformPasswordHasher();
        var hash = hasher.HashPassword(Password);
        hasher.VerifyPassword(hash, Password);
        hasher.SimulateVerification(Password);

        var hashTimes = new List<double>();
        var verifyTimes = new List<double>();
        var simulateTimes = new List<double>();
        for (var round = 0; round < Rounds; round++)
        {
            hashTimes.Add(Time(() => hasher.HashPassword(Password)));

            // Đổi thứ tự mỗi vòng để không thiên lệch theo vị trí.
            if (round % 2 == 0)
            {
                verifyTimes.Add(Time(() => hasher.VerifyPassword(hash, Password)));
                simulateTimes.Add(Time(() => hasher.SimulateVerification(Password)));
            }
            else
            {
                simulateTimes.Add(Time(() => hasher.SimulateVerification(Password)));
                verifyTimes.Add(Time(() => hasher.VerifyPassword(hash, Password)));
            }
        }

        output.WriteLine($"Argon2id m={PlatformPasswordHasher.MemoryKib}KiB t={PlatformPasswordHasher.Iterations} p={PlatformPasswordHasher.Parallelism}, salt {PlatformPasswordHasher.SaltSize}B, hash {PlatformPasswordHasher.HashSize}B");
        output.WriteLine($"hash     median {Median(hashTimes):F0} ms (min {hashTimes.Min():F0}, max {hashTimes.Max():F0})");
        output.WriteLine($"verify   median {Median(verifyTimes):F0} ms (min {verifyTimes.Min():F0}, max {verifyTimes.Max():F0})");
        output.WriteLine($"simulate median {Median(simulateTimes):F0} ms (min {simulateTimes.Min():F0}, max {simulateTimes.Max():F0})");
        Assert.InRange(Median(verifyTimes), 1, 5_000);
        Assert.InRange(Median(simulateTimes) / Median(verifyTimes), 0.5, 2.0);
    }

    private static double Time(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }
}
