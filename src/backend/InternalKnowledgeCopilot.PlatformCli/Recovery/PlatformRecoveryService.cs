using System.Globalization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.Bootstrap;
using InternalKnowledgeCopilot.PlatformCli.Preflight;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.PlatformCli.Recovery;

public sealed record RecoveryPreflight(
    OperationResult Result,
    PlatformDatabaseInspection? Inspection,
    ProvisioningAuthorityRecord? Record,
    RecoveryPlan? Plan)
{
    public PlatformAdminSummary? Target => Plan?.TargetAdminId is { } id ? Inspection?.Admins.SingleOrDefault(admin => admin.Id == id) : null;
}

// Recovery (C5) chỉ chạy khi provisioning authority độc lập đã cấp RecoveryPending cho đúng deployment. Không chạy lại bootstrap,
// không đổi state Completed (chỉ tăng state_version), không xóa/sửa audit cũ. Mỗi recovery:
// - AdminRecovery: đặt mật khẩu mới do operator nhập, must_change_password=true, mở khóa/kích hoạt lại, token_version+1
//   (mọi JWT cũ bị PlatformPrincipalValidator từ chối).
// - BootstrapReconciliation: không đổi credential, token_version+1, rồi kích hoạt authority với bằng chứng bootstrap từ SQLite.
// Trong suốt RecoveryPending runtime từ chối mọi platform auth; recovery bị gián đoạn để lại đúng trạng thái đó.
public sealed class PlatformRecoveryService(PlatformCliContext context, IProvisioningAuthorityMutator mutator)
{
    public async Task<RecoveryPreflight> PreflightAsync(CancellationToken cancellationToken = default)
    {
        var settings = context.Authority!;
        var inspection = await context.InspectAsync(cancellationToken);
        if (inspection is null)
        {
            return new(OperationResult.Denied("database_not_found"), null, null, null);
        }

        var read = await context.GetReader().ReadAsync(cancellationToken);
        if (read.Outcome == ProvisioningAuthorityReadOutcome.Unavailable)
        {
            return new(OperationResult.Unavailable(read.Reason), inspection, null, null);
        }

        if (read.Record is not { } record)
        {
            return new(OperationResult.Denied(read.Outcome == ProvisioningAuthorityReadOutcome.NotFound ? "authority_not_authorized" : read.Reason), inspection, null, null);
        }

        if (!record.Matches(settings.Identity))
        {
            return new(OperationResult.Denied("authority_identity_mismatch"), inspection, record, null);
        }

        if (record.Status != ProvisioningAuthorityStatus.RecoveryPending)
        {
            return new(OperationResult.Denied(record.Status == ProvisioningAuthorityStatus.Revoked ? "authority_revoked" : "recovery_not_authorized"), inspection, record, null);
        }

        if (inspection.State is { } state && state.DeploymentId != settings.Identity.DeploymentId)
        {
            return new(OperationResult.Denied("deployment_identity_mismatch"), inspection, record, null);
        }

        var plan = ProvisioningReconciliation.PlanRecovery(inspection, record);
        return plan.Kind == RecoveryPlanKind.Reject
            ? new(OperationResult.Denied(plan.Reason), inspection, record, plan)
            : new(OperationResult.Success(plan.Reason), inspection, record, plan);
    }

    // newPasswordHash bắt buộc với AdminRecovery và phải null với BootstrapReconciliation.
    public async Task<OperationResult> ApplyAsync(string? newPasswordHash, string requestId, CancellationToken cancellationToken = default)
    {
        var preflight = await PreflightAsync(cancellationToken);
        if (preflight.Result.Outcome != OperationOutcome.Success)
        {
            return preflight.Result;
        }

        var plan = preflight.Plan!;
        var record = preflight.Record!;
        if (plan.Kind != RecoveryPlanKind.Apply)
        {
            return OperationResult.Denied("recovery_already_applied");
        }

        var isAdminRecovery = plan.Purpose == ProvisioningRecoveryPurpose.AdminRecovery;
        if (isAdminRecovery == string.IsNullOrEmpty(newPasswordHash))
        {
            throw new InvalidOperationException("recovery_credential_input_invalid");
        }

        var recoveryId = record.RecoveryId!.Value;
        var now = AuthorityTimestamp.Now();
        try
        {
            await using var db = context.CreateDbContext(readOnly: false);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var state = await db.PlatformSecurityState.SingleAsync(cancellationToken);
            if (state.StateVersion != plan.ExpectedStateVersion || state.BootstrapStatus != PlatformBootstrapStatus.Completed)
            {
                return OperationResult.Denied("state_version_mismatch");
            }

            // Chống lặp/đồng thời: transaction BEGIN IMMEDIATE tuần tự hóa các CLI; recovery đã có audit thì không áp dụng lại.
            if (await PlatformDatabaseInspector.IsRecoveryAppliedAsync(db, recoveryId, cancellationToken))
            {
                return OperationResult.Denied("recovery_already_applied");
            }

            var admin = await db.PlatformAdmins.SingleAsync(item => item.Id == plan.TargetAdminId, cancellationToken);
            if (isAdminRecovery)
            {
                admin.PasswordHash = newPasswordHash!;
                admin.MustChangePassword = true;
                admin.Status = PlatformAdminStatus.Active;
                admin.FailedLoginCount = 0;
                admin.LockedUntil = null;
            }

            admin.TokenVersion += 1;
            admin.UpdatedAt = now;
            state.StateVersion += 1;
            state.UpdatedAt = now;

            var purposeCode = isAdminRecovery ? "admin_recovery" : "bootstrap_reconciliation";
            var auditWriter = new PlatformAuditWriter(db);
            auditWriter.Stage(new PlatformAuditEntry(
                isAdminRecovery ? PlatformAuditActions.AdminRecovered : PlatformAuditActions.BootstrapReconciled,
                PlatformAuditOutcome.Success,
                null,
                requestId,
                new Dictionary<string, string>
                {
                    [PlatformAuditMetadataKeys.RecoveryId] = recoveryId.ToString("D"),
                    [PlatformAuditMetadataKeys.RecoveryPurpose] = purposeCode,
                    [PlatformAuditMetadataKeys.StateVersion] = state.StateVersion.ToString(CultureInfo.InvariantCulture),
                    [PlatformAuditMetadataKeys.TokenVersion] = admin.TokenVersion.ToString(CultureInfo.InvariantCulture),
                    [PlatformAuditMetadataKeys.AuthorityProvider] = context.Authority!.ProviderCode,
                },
                TargetPlatformAdminId: admin.Id));
            auditWriter.Stage(new PlatformAuditEntry(
                PlatformAuditActions.TokensRevoked,
                PlatformAuditOutcome.Success,
                null,
                requestId,
                new Dictionary<string, string>
                {
                    [PlatformAuditMetadataKeys.Reason] = purposeCode,
                    [PlatformAuditMetadataKeys.TokenVersion] = admin.TokenVersion.ToString(CultureInfo.InvariantCulture),
                },
                TargetPlatformAdminId: admin.Id));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Denied("state_version_conflict");
        }
        catch (Exception)
        {
            // Rollback hoặc không rõ commit: authority vẫn RecoveryPending nên runtime vẫn từ chối; recovery-check sẽ phân loại.
            return OperationResult.Reconcile("recovery_transaction_failed");
        }

        return await FinalizeCoreAsync(record, plan, CancellationToken.None);
    }

    // Recovery đã áp dụng vào SQLite (audit có recovery_id, state_version = prior+1) nhưng authority chưa kích hoạt lại.
    public async Task<OperationResult> FinalizeAsync(CancellationToken cancellationToken = default)
    {
        var preflight = await PreflightAsync(cancellationToken);
        if (preflight.Result.Outcome != OperationOutcome.Success)
        {
            return preflight.Result;
        }

        return preflight.Plan!.Kind == RecoveryPlanKind.FinalizeOnly
            ? await FinalizeCoreAsync(preflight.Record!, preflight.Plan, CancellationToken.None)
            : OperationResult.Denied("recovery_not_applied");
    }

    private async Task<OperationResult> FinalizeCoreAsync(ProvisioningAuthorityRecord record, RecoveryPlan plan, CancellationToken cancellationToken)
    {
        var settings = context.Authority!;
        var inspection = await context.InspectAsync(cancellationToken);
        var state = inspection?.State;
        if (state is null || state.StateVersion != plan.ExpectedStateVersion + 1)
        {
            return OperationResult.Reconcile("recovery_state_not_applied");
        }

        var now = AuthorityTimestamp.Now();
        var activated = record with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = record.Version + 1,
            StateVersion = state.StateVersion,
            RecoveryId = null,
            RecoveryPurpose = null,
            RecoveryTargetAdminId = null,
            RecoveryAuthorizedAt = null,
            LastRecoveryId = record.RecoveryId,
            LastRecoveredAt = now,
            RecoveryCount = record.RecoveryCount + 1,
        };
        if (plan.Purpose == ProvisioningRecoveryPurpose.BootstrapReconciliation)
        {
            activated = activated with
            {
                BootstrapAdminId = record.ReservedAdminId,
                BootstrapCompletedAt = state.BootstrapCompletedAt,
                ActivatedAt = now,
            };
        }

        string? failure = null;
        try
        {
            if (await mutator.TransitionAsync(record, activated, cancellationToken) == AuthorityTransitionOutcome.ConditionFailed)
            {
                failure = "authority_finalize_conflict";
            }
        }
        catch (ProvisioningAuthorityUnavailableException ex)
        {
            failure = ex.Reason;
        }

        // Lần finalize đồng thời khác đã thành công cũng cho kết quả verified; ngược lại không báo thành công.
        var verification = ProvisioningEvidence.EvaluateRuntime(
            (await context.InspectAsync(CancellationToken.None))?.State,
            await context.GetReader().ReadAsync(CancellationToken.None),
            settings.Identity);
        return verification.Verified
            ? OperationResult.Success("recovery_completed")
            : OperationResult.Reconcile(failure ?? verification.Reason);
    }
}
