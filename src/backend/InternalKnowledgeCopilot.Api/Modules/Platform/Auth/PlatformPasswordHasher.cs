using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

public interface IPlatformPasswordHasher
{
    string HashPassword(string password);

    // Trả false (không ném lỗi) với hash sai định dạng, thuật toán/tham số không hỗ trợ hoặc định dạng PBKDF2 của tenant.
    bool VerifyPassword(string? passwordHash, string password);

    // Tốn thời gian tương đương một lần verify thật, dùng khi không có tài khoản để tránh lộ qua timing.
    void SimulateVerification(string password);
}

// Argon2id (RFC 9106) qua BouncyCastle.Cryptography, lưu theo định dạng PHC chuẩn:
// $argon2id$v=19$m=<KiB>,t=<iterations>,p=<parallelism>$<salt base64>$<hash base64>, khớp CHECK của S01-A.
// Không dùng lại PasswordHasher (PBKDF2) của tenant.
public sealed class PlatformPasswordHasher : IPlatformPasswordHasher
{
    public const string Algorithm = "argon2id";
    public const int Argon2Version = 19;
    public const int MemoryKib = 65_536;
    public const int Iterations = 3;
    public const int Parallelism = 1;
    public const int SaltSize = 16;
    public const int HashSize = 32;

    // Giới hạn khi đọc hash từ DB để một bản ghi bị sửa không thể buộc server cấp phát bộ nhớ/CPU quá lớn.
    private const int MinMemoryKib = 19_456;
    private const int MaxMemoryKib = 262_144;
    private const int MaxIterations = 10;
    private const int MaxParallelism = 4;
    private const int MinSaltSize = 16;
    private const int MaxSaltSize = 64;
    private const int MaxHashSize = 64;

    private static readonly byte[] SimulationSalt = RandomNumberGenerator.GetBytes(SaltSize);

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, MemoryKib, Iterations, Parallelism, HashSize);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"${Algorithm}$v={Argon2Version}$m={MemoryKib},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    public bool VerifyPassword(string? passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (!TryParse(passwordHash, out var parsed))
        {
            SimulateVerification(password);
            return false;
        }

        var actual = Derive(password, parsed.Salt, parsed.MemoryKib, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash);
    }

    public void SimulateVerification(string password)
    {
        _ = Derive(password ?? string.Empty, SimulationSalt, MemoryKib, Iterations, Parallelism, HashSize);
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int hashSize)
    {
        var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13)
            .WithMemoryAsKB(memoryKib)
            .WithIterations(iterations)
            .WithParallelism(parallelism)
            .WithSalt(salt)
            .Build();
        var generator = new Argon2BytesGenerator();
        generator.Init(parameters);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var output = new byte[hashSize];
            generator.GenerateBytes(passwordBytes, output);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static bool TryParse(string? value, out ParsedHash parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(value) || value.Length > 1024)
        {
            return false;
        }

        var parts = value.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0 || parts[1] != Algorithm || parts[2] != $"v={Argon2Version}")
        {
            return false;
        }

        var settings = parts[3].Split(',');
        if (settings.Length != 3
            || !TryReadSetting(settings[0], "m=", out var memoryKib)
            || !TryReadSetting(settings[1], "t=", out var iterations)
            || !TryReadSetting(settings[2], "p=", out var parallelism)
            || memoryKib is < MinMemoryKib or > MaxMemoryKib
            || iterations is < 1 or > MaxIterations
            || parallelism is < 1 or > MaxParallelism
            || !TryDecode(parts[4], out var salt)
            || !TryDecode(parts[5], out var hash)
            || salt.Length is < MinSaltSize or > MaxSaltSize
            || hash.Length is < HashSize or > MaxHashSize)
        {
            return false;
        }

        parsed = new ParsedHash(memoryKib, iterations, parallelism, salt, hash);
        return true;
    }

    private static bool TryReadSetting(string value, string prefix, out int result)
    {
        result = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    // PHC dùng base64 chuẩn không padding.
    private static string Encode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=');
    }

    private static bool TryDecode(string value, out byte[] result)
    {
        result = [];
        if (value.Length == 0 || value.Contains('='))
        {
            return false;
        }

        var padded = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
        var buffer = new byte[padded.Length];
        if (!Convert.TryFromBase64String(padded, buffer, out var written))
        {
            return false;
        }

        result = buffer[..written];
        return true;
    }

    private readonly record struct ParsedHash(int MemoryKib, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
