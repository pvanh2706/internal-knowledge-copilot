using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

// Kết quả quyết định của một handler: hoặc từ chối (không ghi gì), hoặc tập ghi/kiểm tra có điều kiện cần commit nguyên tử.
public sealed record AuthorityDecision
{
    public required bool Accepted { get; init; }

    public required string Reason { get; init; }

    public int RejectStatusCode { get; init; } = 409;

    public IReadOnlyList<AuthorityStoreWrite> Writes { get; init; } = [];

    public IReadOnlyList<AuthorityStoreCheck> Checks { get; init; } = [];

    public Guid? OperationId { get; init; }

    public string? ApproverPersonId { get; init; }

    public long? VersionBefore { get; init; }

    public long? VersionAfter { get; init; }

    public static AuthorityDecision Reject(string reason, int statusCode = 409) => new() { Accepted = false, Reason = reason, RejectStatusCode = statusCode };
}

// Đọc trạng thái hiện tại phục vụ quyết định. Bản ghi authority hỏng hoặc thuộc authority khác ⇒ từ chối (fail closed).
internal static class AuthorityStateReader
{
    public static async Task<(ProvisioningAuthorityRecord? Record, string? Problem)> ReadRecordAsync(
        IAuthorityStore store,
        AuthorityServicePolicy policy,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var read = DynamoDbAuthorityItem.Parse(await store.GetAsync(AuthorityStoreKeys.Deployment(deploymentId), cancellationToken), deploymentId);
        return read.Outcome switch
        {
            ProvisioningAuthorityReadOutcome.NotFound => (null, null),
            ProvisioningAuthorityReadOutcome.Found when !string.Equals(read.Record!.AuthorityId, policy.AuthorityId, StringComparison.Ordinal)
                => (null, "authority_identity_mismatch"),
            ProvisioningAuthorityReadOutcome.Found => (read.Record, null),
            _ => (null, "authority_record_invalid"),
        };
    }

    public static async Task<(AuthorityApproval Approval, long Version)?> ReadApprovalAsync(IAuthorityStore store, Guid operationId, CancellationToken cancellationToken)
    {
        return AuthorityDocuments.FromItem<AuthorityApproval>(await store.GetAsync(AuthorityStoreKeys.Approval(operationId), cancellationToken), AuthorityDocuments.ApprovalType);
    }

    public static AuthorityStoreWrite RecordWrite(ProvisioningAuthorityRecord next, long? expectedVersion)
    {
        return new AuthorityStoreWrite(AuthorityStoreKeys.Deployment(next.DeploymentId), DynamoDbAuthorityItem.ToAttributes(next), expectedVersion);
    }

    public static AuthorityStoreWrite ApprovalWrite(AuthorityApproval approval, long? expectedVersion)
    {
        return new AuthorityStoreWrite(
            AuthorityStoreKeys.Approval(approval.OperationId),
            AuthorityDocuments.ToItem(approval, AuthorityDocuments.ApprovalType, (expectedVersion ?? 0) + 1),
            expectedVersion);
    }
}
