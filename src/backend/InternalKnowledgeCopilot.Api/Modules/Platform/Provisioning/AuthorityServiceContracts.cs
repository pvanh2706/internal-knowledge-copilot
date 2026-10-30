using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

// S01-C-REM R1/R2: hợp đồng wire giữa CLI operator / công cụ approver và Authority Service phía server (S01-C-ADR-01).
// Chỉ là DTO và hằng số route: runtime API không gọi Authority Service và không có quyền ghi authority.
// Danh tính người gọi KHÔNG bao giờ nằm trong body; Authority Service lấy nó từ ngữ cảnh đã được AWS xác thực.
public static class AuthorityServiceRoutes
{
    // Ranh giới ủy quyền riêng: route approval chỉ cho Security Approver, route operation chỉ cho Deployment Operator.
    public const string Approvals = "POST /authority/approvals";
    public const string Reserve = "POST /authority/operations/reserve";
    public const string Activate = "POST /authority/operations/activate";
    public const string ClaimRecovery = "POST /authority/operations/claim-recovery";
    public const string CompleteRecovery = "POST /authority/operations/complete-recovery";

    public static readonly IReadOnlySet<string> OperatorRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        Reserve, Activate, ClaimRecovery, CompleteRecovery,
    };

    public static readonly IReadOnlySet<string> ApproverRoutes = new HashSet<string>(StringComparer.Ordinal) { Approvals };

    public static string PathOf(string route) => route["POST ".Length..];
}

public enum AuthorityApprovalAction
{
    // Lần cấp bootstrap đầu tiên của deployment (không có bản ghi và không có lịch sử cấp phát).
    Bootstrap,

    // Cấp lại sau reservation bị gián đoạn không có bằng chứng SQLite, hoặc thay approval bootstrap đã hết hạn chưa dùng.
    ReauthorizeBootstrap,
    AdminRecovery,
    BootstrapReconciliation,

    // Thu hồi (hướng fail-closed); không có route nào gỡ Revoked.
    Revoke,
}

public sealed record AuthorityApprovalRequest
{
    public required string RequestId { get; init; }

    public required Guid DeploymentId { get; init; }

    public required AuthorityApprovalAction Action { get; init; }

    // Version authority mà approver đã xem; null chỉ hợp lệ với Bootstrap lần đầu.
    public long? ExpectedAuthorityVersion { get; init; }

    public string? AssignedOperatorPersonId { get; init; }

    public Guid? TargetAdminId { get; init; }

    public int? ExpiresInMinutes { get; init; }
}

// OperationId là định danh do Authority Service cấp khi phê duyệt: authorization_id (bootstrap), reservation_id (sau reserve)
// hoặc recovery_id (recovery) trong bản ghi authority.
public sealed record AuthorityOperationRequest
{
    public required string RequestId { get; init; }

    public required Guid DeploymentId { get; init; }

    public required Guid OperationId { get; init; }

    public required long ExpectedAuthorityVersion { get; init; }

    public Guid? ReservedAdminId { get; init; }

    public DateTimeOffset? BootstrapCompletedAt { get; init; }

    public int? StateVersion { get; init; }
}

public enum AuthorityResponseOutcome
{
    Applied,

    // Cùng request_id và cùng nội dung đã được áp dụng trước đó; không áp dụng lại.
    Replayed,
    Rejected,

    // Không biết thay đổi đã được ghi hay chưa: caller phải thử lại với cùng request_id hoặc đọc lại authority.
    OutcomeUnknown,
}

public sealed record AuthorityServiceResponse
{
    public required AuthorityResponseOutcome Outcome { get; init; }

    public required string Reason { get; init; }

    public string? RequestId { get; init; }

    public Guid? OperationId { get; init; }

    // person_id đã được Authority Service xác minh từ principal AWS của caller (không phải giá trị do client khai).
    public string? CallerPersonId { get; init; }

    public string? ApproverPersonId { get; init; }

    public long? AuthorityVersion { get; init; }
}

public static class AuthorityServiceJson
{
    // Thuộc tính lạ (ví dụ "caller_person_id", "role") làm request bị từ chối thay vì bị bỏ qua.
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };
}
