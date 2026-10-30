using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

// Lỗi hạ tầng khi ghi authority (timeout, access denied, không khóa được file...). Mã lý do an toàn để in ra CLI.
public sealed class ProvisioningAuthorityUnavailableException(string reason, Exception? innerException = null)
    : Exception(reason, innerException)
{
    public string Reason { get; } = reason;
}

// Protected Local Seal (chỉ Development/Testing): file JSON chứa bản ghi authority, toàn vẹn bằng HMAC-SHA256 với key cấu hình.
// Giới hạn: seal nằm trên cùng máy nên một bản restore/rollback toàn máy (hoặc ai đọc được key cấu hình) làm giả/khôi phục được
// nó; không bao giờ là bằng chứng production.
public static class LocalSealFile
{
    public const string Format = "ikc-platform-provisioning-local-seal";
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ProvisioningAuthorityReadResult Read(string path, byte[] integrityKey)
    {
        byte[] content;
        try
        {
            if (!File.Exists(path))
            {
                return ProvisioningAuthorityReadResult.NotFound();
            }

            content = ReadAllBytesShared(path);
        }
        catch (FileNotFoundException)
        {
            return ProvisioningAuthorityReadResult.NotFound();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ProvisioningAuthorityReadResult.Unavailable("local_seal_unreadable");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<SealEnvelope>(content, JsonOptions);
            if (envelope is null || envelope.Format != Format || envelope.SchemaVersion != SchemaVersion)
            {
                return ProvisioningAuthorityReadResult.Malformed("local_seal_format_invalid");
            }

            var payload = Convert.FromBase64String(envelope.Payload);
            var mac = Convert.FromBase64String(envelope.Mac);
            if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(integrityKey, payload), mac))
            {
                return ProvisioningAuthorityReadResult.Malformed("local_seal_integrity_failed");
            }

            var record = JsonSerializer.Deserialize<ProvisioningAuthorityRecord>(payload, JsonOptions);
            if (record is null)
            {
                return ProvisioningAuthorityReadResult.Malformed("local_seal_record_invalid");
            }

            return record.Validate() is { } invalid
                ? ProvisioningAuthorityReadResult.Malformed(invalid)
                : ProvisioningAuthorityReadResult.Found(record);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException or ArgumentException)
        {
            return ProvisioningAuthorityReadResult.Malformed("local_seal_record_invalid");
        }
    }

    // Ghi nguyên tử: file tạm cùng thư mục rồi thay thế. Caller phải giữ khóa (AcquireLock) khi đọc-sửa-ghi.
    public static void Write(string path, byte[] integrityKey, ProvisioningAuthorityRecord record)
    {
        if (record.Validate() is { } invalid)
        {
            throw new InvalidOperationException(invalid);
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        var envelope = new SealEnvelope(Format, SchemaVersion, Convert.ToBase64String(payload), Convert.ToBase64String(HMACSHA256.HashData(integrityKey, payload)));
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    // Khóa độc quyền liên tiến trình cho thao tác đọc-sửa-ghi (tương đương conditional write của DynamoDB).
    public static async Task<IDisposable> AcquireLockAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream($"{path}.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 40)), cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ProvisioningAuthorityUnavailableException("local_seal_lock_unavailable", ex);
            }
        }
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            catch (IOException) when (attempt < 20 && File.Exists(path))
            {
                Thread.Sleep(10);
            }
        }
    }

    private sealed record SealEnvelope(string Format, int SchemaVersion, string Payload, string Mac);
}

public sealed class LocalSealProvisioningAuthorityReader(ProvisioningAuthoritySettings settings) : IProvisioningAuthorityReader
{
    public Task<ProvisioningAuthorityReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LocalSealFile.Read(settings.LocalSealPath!, settings.LocalSealIntegrityKey!));
    }
}
