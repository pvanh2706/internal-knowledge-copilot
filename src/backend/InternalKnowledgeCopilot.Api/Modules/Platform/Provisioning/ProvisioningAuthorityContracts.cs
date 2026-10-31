namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

public enum ProvisioningAuthorityKind
{
    LocalSeal,
    DynamoDb,
}

// Cấu hình đã được ProvisioningAuthorityConfigurationValidator kiểm tra; chỉ tồn tại khi Provider khác None.
public sealed record ProvisioningAuthoritySettings(
    ProvisioningAuthorityKind Kind,
    ProvisioningAuthorityIdentity Identity,
    string? LocalSealPath = null,
    byte[]? LocalSealIntegrityKey = null,
    string? DynamoDbTableName = null,
    string? DynamoDbRegion = null,
    TimeSpan? DynamoDbTimeout = null,
    string? DynamoDbOperatorProfile = null,
    Uri? AuthorityServiceEndpoint = null)
{
    public string ProviderCode => Kind == ProvisioningAuthorityKind.DynamoDb ? "dynamodb" : "local_seal";
}

public enum ProvisioningAuthorityReadOutcome
{
    Found,
    NotFound,
    Unavailable,
    Malformed,
}

public sealed record ProvisioningAuthorityReadResult(ProvisioningAuthorityReadOutcome Outcome, ProvisioningAuthorityRecord? Record, string Reason)
{
    public static ProvisioningAuthorityReadResult Found(ProvisioningAuthorityRecord record) => new(ProvisioningAuthorityReadOutcome.Found, record, "found");

    public static ProvisioningAuthorityReadResult NotFound() => new(ProvisioningAuthorityReadOutcome.NotFound, null, "authority_record_missing");

    public static ProvisioningAuthorityReadResult Unavailable(string reason) => new(ProvisioningAuthorityReadOutcome.Unavailable, null, reason);

    public static ProvisioningAuthorityReadResult Malformed(string reason) => new(ProvisioningAuthorityReadOutcome.Malformed, null, reason);
}

// Đọc bằng chứng authority. Runtime API chỉ có interface này (không có đường ghi); mọi lỗi hạ tầng được chuyển thành
// Unavailable thay vì ném ra, để caller fail closed một cách xác định.
public interface IProvisioningAuthorityReader
{
    Task<ProvisioningAuthorityReadResult> ReadAsync(CancellationToken cancellationToken = default);
}
