using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

public sealed record ProvisioningEvidenceResult(bool Verified, string Reason)
{
    public static readonly ProvisioningEvidenceResult Ok = new(true, "verified");

    public static ProvisioningEvidenceResult Denied(string reason) => new(false, reason);
}

// Quy tắc duy nhất quyết định platform auth có được phép hay không; API (mỗi request) và CLI (xác minh sau bootstrap/
// recovery, status) dùng cùng logic. Chỉ Activated với danh tính deployment/authority, admin bootstrap, thời điểm hoàn tất
// và state_version khớp chính xác với SQLite mới được chấp nhận. Mọi trường hợp khác fail closed.
public static class ProvisioningEvidence
{
    public static ProvisioningEvidenceResult EvaluateRuntime(
        PlatformSecurityStateEntity? state,
        ProvisioningAuthorityReadResult read,
        ProvisioningAuthorityIdentity expected)
    {
        if (read.Outcome != ProvisioningAuthorityReadOutcome.Found || read.Record is null)
        {
            return ProvisioningEvidenceResult.Denied(read.Reason);
        }

        var record = read.Record;
        if (record.Validate() is { } invalid)
        {
            return ProvisioningEvidenceResult.Denied(invalid);
        }

        if (!record.Matches(expected))
        {
            return ProvisioningEvidenceResult.Denied("authority_identity_mismatch");
        }

        if (record.Status != ProvisioningAuthorityStatus.Activated)
        {
            return ProvisioningEvidenceResult.Denied(record.Status switch
            {
                ProvisioningAuthorityStatus.Revoked => "authority_revoked",
                ProvisioningAuthorityStatus.RecoveryPending => "authority_recovery_pending",
                ProvisioningAuthorityStatus.Reserved => "authority_activation_pending",
                _ => "authority_not_activated",
            });
        }

        if (state is null || state.BootstrapStatus != PlatformBootstrapStatus.Completed || state.BootstrapAdminId is null)
        {
            return ProvisioningEvidenceResult.Denied("bootstrap_not_completed");
        }

        if (state.DeploymentId != expected.DeploymentId)
        {
            return ProvisioningEvidenceResult.Denied("deployment_identity_mismatch");
        }

        if (record.BootstrapAdminId != state.BootstrapAdminId || !AuthorityTimestamp.AreEqual(record.BootstrapCompletedAt, state.BootstrapCompletedAt))
        {
            return ProvisioningEvidenceResult.Denied("bootstrap_evidence_mismatch");
        }

        // Phát hiện restore một snapshot Completed cũ hơn lần recovery gần nhất (hoặc state bị sửa ngoài CLI).
        // Không phát hiện được snapshot cùng state_version (C-G09).
        return record.StateVersion == state.StateVersion
            ? ProvisioningEvidenceResult.Ok
            : ProvisioningEvidenceResult.Denied("state_version_mismatch");
    }
}
