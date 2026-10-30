using System.Collections.Concurrent;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.PlatformCli.Authority;

public enum AuthorityStepOutcome
{
    Applied,

    // Không áp dụng (chắc chắn). Reason là mã an toàn từ Authority Service hoặc client.
    Rejected,

    // Có thể đã áp dụng hoặc chưa: thử lại với cùng request_id, rồi đọc lại authority. Không bao giờ đoán.
    Unknown,
}

// Danh tính người thực hiện để ghi vào audit SQLite (C-IR-04). Với DynamoDb, person_id do Authority Service xác minh từ
// principal AWS; với LocalSeal (chỉ Development/Testing) không có authority độc lập nên danh tính được đánh dấu là chưa xác minh.
public sealed record OperatorIdentity(string PersonId, string Assurance, string? ApproverPersonId)
{
    public const string ServiceVerified = "authority_service_verified";
    public const string LocalSealUnverified = "local_seal_unverified";

    public static OperatorIdentity UnverifiedLocal() => new("local-seal-operator", LocalSealUnverified, null);
}

public sealed record AuthorityStepResult(AuthorityStepOutcome Outcome, string Reason, OperatorIdentity? Operator = null)
{
    public static AuthorityStepResult Applied(string reason, OperatorIdentity? identity) => new(AuthorityStepOutcome.Applied, reason, identity);

    public static AuthorityStepResult Rejected(string reason) => new(AuthorityStepOutcome.Rejected, reason);

    public static AuthorityStepResult Unknown(string reason) => new(AuthorityStepOutcome.Unknown, reason);
}

// Các bước operator được phép (S01-C-REM R2). CLI không quyết định trạng thái authority: production gửi yêu cầu tới
// Authority Service, nơi sơ đồ chuyển trạng thái, approval và danh tính được cưỡng chế.
public interface IAuthorityTransitionClient
{
    Task<AuthorityStepResult> ReserveAsync(ProvisioningAuthorityRecord authorized, Guid reservedAdminId, string requestId, CancellationToken cancellationToken = default);

    Task<AuthorityStepResult> ActivateAsync(ProvisioningAuthorityRecord reserved, DateTimeOffset bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default);

    Task<AuthorityStepResult> ClaimRecoveryAsync(ProvisioningAuthorityRecord pending, string requestId, CancellationToken cancellationToken = default);

    Task<AuthorityStepResult> CompleteRecoveryAsync(
        ProvisioningAuthorityRecord pending,
        int stateVersion,
        DateTimeOffset? bootstrapCompletedAt,
        string requestId,
        CancellationToken cancellationToken = default);
}

public static class AuthorityCalls
{
    public const int MaxAttempts = 3;

    public static string NewRequestId(string step) => $"cli-{step}-{Guid.NewGuid():N}";

    // Kết quả không rõ (timeout, mất phản hồi): thử lại với CÙNG request_id. Authority Service trả lại kết quả đã ghi trong
    // ledger thay vì áp dụng lần hai; nếu vẫn không rõ, caller phải đọc lại authority để quyết định.
    public static async Task<AuthorityStepResult> WithRetryAsync(Func<string, Task<AuthorityStepResult>> call, string requestId)
    {
        var result = await call(requestId);
        for (var attempt = 2; attempt <= MaxAttempts && result.Outcome == AuthorityStepOutcome.Unknown; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            result = await call(requestId);
        }

        return result;
    }
}

// LocalSeal (CHỈ Development/Testing, validator chặn ở môi trường khác): không có Authority Service nên CLI tự áp dụng chuyển
// trạng thái dưới khóa file. Đây KHÔNG phải ranh giới bảo mật: ai có integrity key đều ghi được seal (C-IR-01 chỉ được khắc
// phục cho DynamoDb qua Authority Service). Lặp lại cùng request_id trả về cùng kết quả.
public sealed class LocalSealTransitionClient(ProvisioningAuthoritySettings settings) : IAuthorityTransitionClient
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, ProvisioningAuthorityRecord> planned = new(StringComparer.Ordinal);

    public Task<AuthorityStepResult> ReserveAsync(ProvisioningAuthorityRecord authorized, Guid reservedAdminId, string requestId, CancellationToken cancellationToken = default)
    {
        var next = planned.GetOrAdd(requestId, _ => authorized with
        {
            Status = ProvisioningAuthorityStatus.Reserved,
            Version = authorized.Version + 1,
            ReservationId = Guid.NewGuid(),
            ReservedAdminId = reservedAdminId,
            ReservedAt = AuthorityTimestamp.Now(),
        });
        return TransitionAsync(authorized, next, "authority_reserved", "authority_reservation_conflict", cancellationToken);
    }

    public Task<AuthorityStepResult> ActivateAsync(ProvisioningAuthorityRecord reserved, DateTimeOffset bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default)
    {
        var next = planned.GetOrAdd(requestId, _ => reserved with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = reserved.Version + 1,
            BootstrapAdminId = reserved.ReservedAdminId,
            BootstrapCompletedAt = AuthorityTimestamp.Truncate(bootstrapCompletedAt),
            ActivatedAt = AuthorityTimestamp.Now(),
            StateVersion = 1,
        });
        return TransitionAsync(reserved, next, "authority_activated", "authority_activation_conflict", cancellationToken);
    }

    public Task<AuthorityStepResult> ClaimRecoveryAsync(ProvisioningAuthorityRecord pending, string requestId, CancellationToken cancellationToken = default)
    {
        var current = LocalSealFile.Read(settings.LocalSealPath!, settings.LocalSealIntegrityKey!);
        return Task.FromResult(current.Record == pending
            ? AuthorityStepResult.Applied("recovery_claimed", OperatorIdentity.UnverifiedLocal())
            : AuthorityStepResult.Rejected("approval_not_current"));
    }

    public Task<AuthorityStepResult> CompleteRecoveryAsync(
        ProvisioningAuthorityRecord pending,
        int stateVersion,
        DateTimeOffset? bootstrapCompletedAt,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var next = planned.GetOrAdd(requestId, _ =>
        {
            var now = AuthorityTimestamp.Now();
            var activated = pending with
            {
                Status = ProvisioningAuthorityStatus.Activated,
                Version = pending.Version + 1,
                StateVersion = stateVersion,
                RecoveryId = null,
                RecoveryPurpose = null,
                RecoveryTargetAdminId = null,
                RecoveryAuthorizedAt = null,
                LastRecoveryId = pending.RecoveryId,
                LastRecoveredAt = now,
                RecoveryCount = pending.RecoveryCount + 1,
            };
            return pending.RecoveryPurpose == ProvisioningRecoveryPurpose.BootstrapReconciliation
                ? activated with { BootstrapAdminId = pending.ReservedAdminId, BootstrapCompletedAt = bootstrapCompletedAt, ActivatedAt = now }
                : activated;
        });
        return TransitionAsync(pending, next, "recovery_completed", "authority_finalize_conflict", cancellationToken);
    }

    private async Task<AuthorityStepResult> TransitionAsync(
        ProvisioningAuthorityRecord expected,
        ProvisioningAuthorityRecord next,
        string appliedReason,
        string conflictReason,
        CancellationToken cancellationToken)
    {
        if (!IsOperatorTransition(expected, next))
        {
            return AuthorityStepResult.Rejected("authority_transition_not_permitted");
        }

        try
        {
            using var sealLock = await LocalSealFile.AcquireLockAsync(settings.LocalSealPath!, LockTimeout, cancellationToken);
            var current = LocalSealFile.Read(settings.LocalSealPath!, settings.LocalSealIntegrityKey!);
            if (current.Outcome == ProvisioningAuthorityReadOutcome.Unavailable)
            {
                return AuthorityStepResult.Unknown(current.Reason);
            }

            if (current.Record == next)
            {
                return AuthorityStepResult.Applied(appliedReason, OperatorIdentity.UnverifiedLocal());
            }

            if (current.Record != expected)
            {
                return AuthorityStepResult.Rejected(conflictReason);
            }

            LocalSealFile.Write(settings.LocalSealPath!, settings.LocalSealIntegrityKey!, next);
            return AuthorityStepResult.Applied(appliedReason, OperatorIdentity.UnverifiedLocal());
        }
        catch (ProvisioningAuthorityUnavailableException ex)
        {
            return AuthorityStepResult.Unknown(ex.Reason);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AuthorityStepResult.Unknown("local_seal_write_failed");
        }
    }

    // Kiểm tra nhất quán phía client cho LocalSeal (không phải ranh giới bảo mật).
    private static bool IsOperatorTransition(ProvisioningAuthorityRecord expected, ProvisioningAuthorityRecord next)
    {
        var permitted = (expected.Status, next.Status) switch
        {
            (ProvisioningAuthorityStatus.Authorized, ProvisioningAuthorityStatus.Reserved) => true,
            (ProvisioningAuthorityStatus.Reserved, ProvisioningAuthorityStatus.Activated) => next.ReservationId == expected.ReservationId,
            (ProvisioningAuthorityStatus.RecoveryPending, ProvisioningAuthorityStatus.Activated) =>
                next.ReservationId == expected.ReservationId && next.LastRecoveryId == expected.RecoveryId,
            _ => false,
        };
        return permitted
            && expected.Validate() is null
            && next.Validate() is null
            && next.Version == expected.Version + 1
            && next.DeploymentId == expected.DeploymentId
            && string.Equals(next.AuthorityId, expected.AuthorityId, StringComparison.Ordinal)
            && string.Equals(next.AuthorizationId, expected.AuthorizationId, StringComparison.Ordinal)
            && next.AuthorizedAt == expected.AuthorizedAt;
    }
}
