using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalKnowledgeCopilot.AuthorityService;

// Sự kiện audit có cấu trúc của Authority Service. Mọi sự kiện (kể cả bị từ chối) mang principal quan sát được; sự kiện
// đã xác minh danh tính mang person_id. Không chứa mật khẩu, credential, chữ ký hay body thô của request.
public sealed record AuthorityAuditEvent
{
    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string Outcome { get; init; }

    public required string Reason { get; init; }

    public string? Route { get; init; }

    public string? RequestId { get; init; }

    public Guid? DeploymentId { get; init; }

    public Guid? OperationId { get; init; }

    public string? PersonId { get; init; }

    public string? Function { get; init; }

    public string? PrincipalRoleArn { get; init; }

    public string? ObservedPrincipalArn { get; init; }

    public string? SessionName { get; init; }

    public string? ApproverPersonId { get; init; }

    public long? AuthorityVersionBefore { get; init; }

    public long? AuthorityVersionAfter { get; init; }
}

public static class AuthorityAuditEventTypes
{
    public const string ApprovalIssued = "authority.approval.issued";
    public const string TransitionApplied = "authority.transition.applied";
    public const string RequestReplayed = "authority.request.replayed";
    public const string RequestRejected = "authority.request.rejected";
    public const string OutcomeUnknown = "authority.request.outcome_unknown";
}

public interface IAuthorityAuditSink
{
    void Emit(AuthorityAuditEvent auditEvent);
}

// Production: một dòng JSON mỗi sự kiện ra stdout (CloudWatch Logs của Lambda). Bản ghi bền vững không thể sửa của các thay
// đổi đã áp dụng là ledger + approval trong bảng authority (ghi cùng transaction).
public sealed class JsonLineAuthorityAuditSink(TextWriter writer) : IAuthorityAuditSink
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object gate = new();

    public void Emit(AuthorityAuditEvent auditEvent)
    {
        var line = JsonSerializer.Serialize(auditEvent, JsonOptions);
        lock (gate)
        {
            writer.WriteLine(line);
            writer.Flush();
        }
    }
}
