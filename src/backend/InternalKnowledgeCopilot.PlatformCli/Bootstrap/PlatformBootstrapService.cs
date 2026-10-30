using System.Globalization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.Preflight;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.PlatformCli.Bootstrap;

public enum OperationOutcome
{
    Success,
    Denied,
    AuthorityUnavailable,
    ReconciliationRequired,
}

public sealed record OperationResult(OperationOutcome Outcome, string Reason)
{
    public static OperationResult Success(string reason) => new(OperationOutcome.Success, reason);

    public static OperationResult Denied(string reason) => new(OperationOutcome.Denied, reason);

    public static OperationResult Unavailable(string reason) => new(OperationOutcome.AuthorityUnavailable, reason);

    public static OperationResult Reconcile(string reason) => new(OperationOutcome.ReconciliationRequired, reason);
}

// PasswordHash đã được băm Argon2id trước khi vào service; service không bao giờ thấy mật khẩu gốc.
public sealed record BootstrapInput(string EmailNormalized, string DisplayName, string PasswordHash);

public sealed record BootstrapPreflight(OperationResult Result, PlatformDatabaseInspection? Inspection, ProvisioningAuthorityRecord? Authorized);

// Bootstrap lần đầu (C4). SQLite và authority không thể chung một transaction nên thứ tự là:
// 1) xác minh Authorized, 2) reserve có điều kiện, 3-4) transaction SQLite (admin + state Completed + audit),
// 5) activate có điều kiện, 6) xác minh lại bằng đúng quy tắc runtime. Gián đoạn ở bất kỳ bước nào sau (2) để lại trạng thái
// mà runtime từ chối (Reserved ≠ Activated) cho tới khi reconciliation được ủy quyền; không bao giờ báo thành công một phần.
public sealed class PlatformBootstrapService(PlatformCliContext context, IProvisioningAuthorityMutator mutator)
{
    // Chỉ đọc; chạy trước khi hỏi operator và chạy lại ngay trước khi reserve.
    public async Task<BootstrapPreflight> PreflightAsync(CancellationToken cancellationToken = default)
    {
        var settings = context.Authority!;
        var inspection = await context.InspectAsync(cancellationToken);
        if (inspection is null)
        {
            return new(OperationResult.Denied("database_not_found"), null, null);
        }

        if (inspection.BootstrapIneligibility is { } ineligible)
        {
            return new(OperationResult.Denied(ineligible), inspection, null);
        }

        var read = await context.GetReader().ReadAsync(cancellationToken);
        var result = read switch
        {
            { Outcome: ProvisioningAuthorityReadOutcome.Unavailable } => OperationResult.Unavailable(read.Reason),
            { Outcome: ProvisioningAuthorityReadOutcome.NotFound } => OperationResult.Denied("authority_not_authorized"),
            { Record: null } => OperationResult.Denied(read.Reason),
            { Record: var record } when !record.Matches(settings.Identity) => OperationResult.Denied("authority_identity_mismatch"),
            { Record.Status: ProvisioningAuthorityStatus.Authorized } => OperationResult.Success("eligible"),
            { Record.Status: ProvisioningAuthorityStatus.Reserved } => OperationResult.Denied("authority_reservation_pending"),
            { Record.Status: ProvisioningAuthorityStatus.Activated } => OperationResult.Denied("authority_already_activated"),
            { Record.Status: ProvisioningAuthorityStatus.RecoveryPending } => OperationResult.Denied("authority_recovery_pending"),
            { Record.Status: ProvisioningAuthorityStatus.Revoked } => OperationResult.Denied("authority_revoked"),
            _ => OperationResult.Denied("authority_status_unknown"),
        };
        return new(result, inspection, result.Outcome == OperationOutcome.Success ? read.Record : null);
    }

    public async Task<OperationResult> ExecuteAsync(BootstrapInput input, string requestId, CancellationToken cancellationToken = default)
    {
        var preflight = await PreflightAsync(cancellationToken);
        if (preflight.Authorized is not { } authorized)
        {
            return preflight.Result;
        }

        var settings = context.Authority!;
        var now = AuthorityTimestamp.Now();
        var adminId = Guid.NewGuid();
        var reserved = authorized with
        {
            Status = ProvisioningAuthorityStatus.Reserved,
            Version = authorized.Version + 1,
            ReservationId = Guid.NewGuid(),
            ReservedAdminId = adminId,
            ReservedAt = now,
        };

        // Bước 2: reserve. Thua race (process khác đã reserve) thì không có gì thay đổi.
        try
        {
            if (await mutator.TransitionAsync(authorized, reserved, cancellationToken) == AuthorityTransitionOutcome.ConditionFailed)
            {
                return OperationResult.Denied("authority_reservation_conflict");
            }
        }
        catch (ProvisioningAuthorityUnavailableException ex)
        {
            // Không biết lần ghi có được áp dụng không: đọc lại để quyết định, không đoán.
            var after = await context.GetReader().ReadAsync(CancellationToken.None);
            if (after.Record != reserved)
            {
                return after.Record == authorized
                    ? OperationResult.Unavailable(ex.Reason)
                    : OperationResult.Reconcile("authority_reservation_state_unknown");
            }
        }

        // Từ đây không dừng giữa chừng theo yêu cầu hủy: mọi bước sau phải chạy tới một trạng thái xác định.
        var sqlite = await CommitSqliteAsync(input, settings, reserved, now, requestId);
        if (sqlite is not null)
        {
            return sqlite;
        }

        // Bước 5: activate.
        var activated = reserved with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = reserved.Version + 1,
            BootstrapAdminId = adminId,
            BootstrapCompletedAt = now,
            ActivatedAt = AuthorityTimestamp.Now(),
            StateVersion = 1,
        };
        string? activationFailure = null;
        try
        {
            if (await mutator.TransitionAsync(reserved, activated, CancellationToken.None) == AuthorityTransitionOutcome.ConditionFailed)
            {
                activationFailure = "authority_activation_conflict";
            }
        }
        catch (ProvisioningAuthorityUnavailableException ex)
        {
            activationFailure = ex.Reason;
        }

        // Bước 6: chỉ báo thành công khi bằng chứng hoàn tất khớp đúng như runtime sẽ kiểm tra.
        var verification = await VerifyAsync(settings);
        return verification.Verified
            ? OperationResult.Success("bootstrap_completed")
            : OperationResult.Reconcile(activationFailure ?? verification.Reason);
    }

    public async Task<ProvisioningEvidenceResult> VerifyAsync(ProvisioningAuthoritySettings settings)
    {
        var inspection = await context.InspectAsync(CancellationToken.None);
        var read = await context.GetReader().ReadAsync(CancellationToken.None);
        return ProvisioningEvidence.EvaluateRuntime(inspection?.State, read, settings.Identity);
    }

    // Bước 3-4: một transaction SQLite (BEGIN IMMEDIATE) tạo đúng một admin, state Completed và audit thành công bắt buộc.
    // Điều kiện bootstrap được kiểm tra lại bên trong transaction nên các CLI chạy đồng thời không thể tạo hai admin
    // kể cả khi authority bị dùng sai; singleton PK của state là chốt chặn cuối.
    private async Task<OperationResult?> CommitSqliteAsync(
        BootstrapInput input,
        ProvisioningAuthoritySettings settings,
        ProvisioningAuthorityRecord reserved,
        DateTimeOffset now,
        string requestId)
    {
        try
        {
            await using var db = context.CreateDbContext(readOnly: false);
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (await db.PlatformSecurityState.AnyAsync() || await db.PlatformAdmins.AnyAsync() || await db.PlatformAuditLogs.AnyAsync())
            {
                return OperationResult.Reconcile("sqlite_not_eligible_after_reservation");
            }

            var adminId = reserved.ReservedAdminId!.Value;
            db.PlatformAdmins.Add(new PlatformAdminEntity
            {
                Id = adminId,
                EmailNormalized = input.EmailNormalized,
                DisplayName = input.DisplayName,
                PasswordHash = input.PasswordHash,
                Status = PlatformAdminStatus.Active,
                MustChangePassword = true,
                TokenVersion = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.PlatformSecurityState.Add(new PlatformSecurityStateEntity
            {
                Id = PlatformSecurityStateEntity.SingletonId,
                DeploymentId = settings.Identity.DeploymentId,
                BootstrapStatus = PlatformBootstrapStatus.Completed,
                BootstrapAdminId = adminId,
                BootstrapCompletedAt = now,
                StateVersion = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
            new PlatformAuditWriter(db).Stage(new PlatformAuditEntry(
                PlatformAuditActions.BootstrapCompleted,
                PlatformAuditOutcome.Success,
                null,
                requestId,
                new Dictionary<string, string>
                {
                    [PlatformAuditMetadataKeys.ReservationId] = reserved.ReservationId!.Value.ToString("D"),
                    [PlatformAuditMetadataKeys.DeploymentId] = settings.Identity.DeploymentId.ToString("D"),
                    [PlatformAuditMetadataKeys.StateVersion] = 1.ToString(CultureInfo.InvariantCulture),
                    [PlatformAuditMetadataKeys.AuthorityProvider] = settings.ProviderCode,
                },
                TargetPlatformAdminId: adminId));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return null;
        }
        catch (Exception)
        {
            // Transaction đã rollback (hoặc không rõ commit): authority vẫn Reserved nên runtime tiếp tục từ chối.
            return OperationResult.Reconcile("sqlite_transaction_failed");
        }
    }
}
