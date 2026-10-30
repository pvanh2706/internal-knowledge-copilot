namespace InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;

// S01-E0 Read-only Containment (DEC-0004, S01 Technical Design Baseline v1.0 §8):
// triển khai ban đầu không được ghi nghiệp vụ ra hệ thống nguồn với bất kỳ principal nào.
// Chính sách cố ý không có config/flag để bật lại; mở outbound write cần thiết kế và PO phê duyệt riêng.
public static class ExternalActionReadOnlyPolicy
{
    // Thực thi action ra hệ thống nguồn (POST /copilot/actions/execute) bị từ chối ở API, service, worker và connector.
    public const string ExecutionBlockedErrorCode = "external_action_execution_blocked";

    // POST /copilot/actions/validate chưa có contract tin cậy chứng minh không có side effect nên fail closed:
    // không gọi source, và thao tác phụ thuộc validation (tạo, duyệt action) bị từ chối.
    public const string ValidationBlockedErrorCode = "external_action_validation_blocked";

    public const string RuleAutoApprovalDisabledReason = "rule_auto_approval_disabled_read_only";
}

public sealed class ExternalActionBlockedException(string errorCode) : InvalidOperationException(errorCode);
