using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

public enum AuthorityApprovalStatus
{
    Issued,

    // Operator được chỉ định đã nhận (single-use); chỉ người đó mới hoàn tất được operation.
    Consumed,
    Completed,

    // Approval hết hạn chưa dùng đã được approver thay bằng approval mới.
    Superseded,
}

// Approval gắn với deployment, action, target, version authority, operator được chỉ định, thời hạn và operation id.
public sealed record AuthorityApproval
{
    public required Guid OperationId { get; init; }

    public required Guid DeploymentId { get; init; }

    public required AuthorityApprovalAction Action { get; init; }

    public Guid? TargetAdminId { get; init; }

    // Version bản ghi authority sau khi phê duyệt; operator phải trình đúng version này.
    public required long AuthorityVersion { get; init; }

    public required string AssignedOperatorPersonId { get; init; }

    public required string ApprovedByPersonId { get; init; }

    public required string ApprovedByPrincipal { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required AuthorityApprovalStatus Status { get; init; }

    public required string IssuingRequestId { get; init; }

    public string? ConsumedByPersonId { get; init; }

    public string? ConsumedByPrincipal { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }

    public string? ConsumedRequestId { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}

// Lịch sử cấp phát bootstrap độc lập (tombstone được bảo vệ): không bao giờ xóa, nên mất/restore bản ghi deployment không
// mở lại được Bootstrap lần đầu.
public sealed record AuthorityIssuanceHistory
{
    public required Guid DeploymentId { get; init; }

    public required int BootstrapIssuanceCount { get; init; }

    public required DateTimeOffset FirstIssuedAt { get; init; }

    public required DateTimeOffset LastIssuedAt { get; init; }

    public required Guid LastOperationId { get; init; }
}

// Ledger bền vững của mọi request đã áp dụng; ghi cùng transaction với thay đổi trạng thái nên tồn tại ⇔ đã áp dụng.
public sealed record AuthorityLedgerEntry
{
    public required string RequestId { get; init; }

    public required string Route { get; init; }

    public required string Fingerprint { get; init; }

    public required string PersonId { get; init; }

    public required string PrincipalArn { get; init; }

    public required Guid DeploymentId { get; init; }

    public Guid? OperationId { get; init; }

    public required string Reason { get; init; }

    public long? AuthorityVersion { get; init; }

    public string? ApproverPersonId { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }
}

public enum AuthorityFunction
{
    Operator,
    Approver,
}

// Do Security Operations quản lý ngoài Authority Service (service chỉ đọc). Mỗi role AWS là role riêng của một người.
public sealed record PrincipalMapping
{
    public required string RoleUniqueId { get; init; }

    public required string RoleArn { get; init; }

    public required string PersonId { get; init; }

    public required AuthorityFunction Function { get; init; }

    public required IReadOnlyList<Guid> DeploymentIds { get; init; }

    public required bool Active { get; init; }
}

public sealed record PrincipalRegistryDocument
{
    public required IReadOnlyList<PrincipalMapping> Mappings { get; init; }
}

public sealed class AuthorityDocumentInvalidException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

// Item phụ (approval, issuance, ledger, registry) lưu dạng: pk, version, record_type, schema_version, document (JSON chặt).
public static class AuthorityDocuments
{
    public const string RecordType = DynamoDbAuthorityItem.RecordType;
    public const string SchemaVersion = DynamoDbAuthorityItem.SchemaVersion;
    public const string Document = "document";
    public const int CurrentSchemaVersion = 1;

    public const string ApprovalType = "authority_approval";
    public const string IssuanceType = "authority_issuance_history";
    public const string LedgerType = "authority_operation_ledger";
    public const string RegistryType = "authority_principal_registry";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static Dictionary<string, AttributeValue> ToItem<T>(T document, string recordType, long version)
    {
        return new Dictionary<string, AttributeValue>
        {
            [AuthorityStoreKeys.Version] = new() { N = version.ToString(CultureInfo.InvariantCulture) },
            [RecordType] = new() { S = recordType },
            [SchemaVersion] = new() { N = CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture) },
            [Document] = new() { S = JsonSerializer.Serialize(document, JsonOptions) },
        };
    }

    // Item thiếu → null. Item sai kiểu/hỏng → AuthorityDocumentInvalidException (fail closed, không coi như vắng mặt).
    public static (T Document, long Version)? FromItem<T>(Dictionary<string, AttributeValue>? item, string recordType)
        where T : class
    {
        if (item is null)
        {
            return null;
        }

        try
        {
            if (!item.TryGetValue(RecordType, out var type) || type.S != recordType
                || !item.TryGetValue(SchemaVersion, out var schema) || schema.N != CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)
                || AuthorityStoreKeys.VersionOf(item) is not { } version || version < 1
                || !item.TryGetValue(Document, out var document) || string.IsNullOrEmpty(document.S))
            {
                throw new AuthorityDocumentInvalidException($"{recordType}_invalid");
            }

            var value = JsonSerializer.Deserialize<T>(document.S, JsonOptions) ?? throw new AuthorityDocumentInvalidException($"{recordType}_invalid");
            return (value, version);
        }
        catch (JsonException)
        {
            throw new AuthorityDocumentInvalidException($"{recordType}_invalid");
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
