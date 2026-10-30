using System.Globalization;
using System.Text.RegularExpressions;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

// Trạng thái logic của bằng chứng provisioning ngoài DB (S01-C):
// Authorized → Reserved → Activated do CLI chuyển (điều kiện theo version); Authorized, RecoveryPending và Revoked chỉ do
// provisioning authority độc lập tạo. Reservation không bao giờ tự hết hạn thành cơ hội bootstrap mới.
public enum ProvisioningAuthorityStatus
{
    Authorized,
    Reserved,
    Activated,
    RecoveryPending,
    Revoked,
}

public enum ProvisioningRecoveryPurpose
{
    // Khôi phục quyền truy cập cho một tài khoản platform cụ thể sau khi đã Activated.
    AdminRecovery,

    // Hoàn tất bootstrap bị gián đoạn sau khi SQLite đã commit nhưng authority chưa được Activated.
    BootstrapReconciliation,
}

public sealed record ProvisioningAuthorityIdentity(Guid DeploymentId, string AuthorityId);

public sealed partial record ProvisioningAuthorityRecord
{
    public const int CurrentSchemaVersion = 1;

    public required Guid DeploymentId { get; init; }

    public required string AuthorityId { get; init; }

    public required ProvisioningAuthorityStatus Status { get; init; }

    // Khóa lạc quan cho mọi chuyển trạng thái có điều kiện; tăng đúng 1 mỗi lần ghi.
    public required long Version { get; init; }

    public required string AuthorizationId { get; init; }

    public required DateTimeOffset AuthorizedAt { get; init; }

    public Guid? ReservationId { get; init; }

    public Guid? ReservedAdminId { get; init; }

    public DateTimeOffset? ReservedAt { get; init; }

    public Guid? BootstrapAdminId { get; init; }

    public DateTimeOffset? BootstrapCompletedAt { get; init; }

    public DateTimeOffset? ActivatedAt { get; init; }

    // state_version của platform_security_state mà authority đã xác nhận; SQLite lệch giá trị này thì fail closed.
    public int? StateVersion { get; init; }

    public Guid? RecoveryId { get; init; }

    public ProvisioningRecoveryPurpose? RecoveryPurpose { get; init; }

    public Guid? RecoveryTargetAdminId { get; init; }

    public DateTimeOffset? RecoveryAuthorizedAt { get; init; }

    public Guid? LastRecoveryId { get; init; }

    public DateTimeOffset? LastRecoveredAt { get; init; }

    public int RecoveryCount { get; init; }

    public bool Matches(ProvisioningAuthorityIdentity identity)
    {
        return DeploymentId == identity.DeploymentId && string.Equals(AuthorityId, identity.AuthorityId, StringComparison.Ordinal);
    }

    // Trả null nếu bản ghi hợp lệ về cấu trúc theo trạng thái; ngược lại trả mã lý do (không chứa dữ liệu bản ghi).
    public string? Validate()
    {
        if (DeploymentId == Guid.Empty || !IsSafeIdentifier(AuthorityId) || !IsSafeIdentifier(AuthorizationId) || Version < 1 || RecoveryCount < 0)
        {
            return "authority_record_base_invalid";
        }

        if ((LastRecoveryId is null) != (LastRecoveredAt is null))
        {
            return "authority_record_recovery_history_invalid";
        }

        var hasReservation = ReservationId is not null && ReservedAdminId is not null && ReservedAt is not null;
        var noReservation = ReservationId is null && ReservedAdminId is null && ReservedAt is null;
        var hasActivation = BootstrapAdminId is not null && BootstrapCompletedAt is not null && ActivatedAt is not null && StateVersion is >= 1;
        var noActivation = BootstrapAdminId is null && BootstrapCompletedAt is null && ActivatedAt is null && StateVersion is null;
        var noPendingRecovery = RecoveryId is null && RecoveryPurpose is null && RecoveryTargetAdminId is null && RecoveryAuthorizedAt is null;

        var valid = Status switch
        {
            ProvisioningAuthorityStatus.Authorized => noReservation && noActivation && noPendingRecovery && LastRecoveryId is null,
            ProvisioningAuthorityStatus.Reserved => hasReservation && noActivation && noPendingRecovery && LastRecoveryId is null,
            ProvisioningAuthorityStatus.Activated => hasReservation && hasActivation && BootstrapAdminId == ReservedAdminId && noPendingRecovery,
            ProvisioningAuthorityStatus.RecoveryPending => RecoveryId is not null && RecoveryAuthorizedAt is not null && RecoveryPurpose switch
            {
                ProvisioningRecoveryPurpose.AdminRecovery => hasReservation && hasActivation && BootstrapAdminId == ReservedAdminId && RecoveryTargetAdminId is not null,
                ProvisioningRecoveryPurpose.BootstrapReconciliation => hasReservation && noActivation && RecoveryTargetAdminId is null && LastRecoveryId is null,
                _ => false,
            },
            ProvisioningAuthorityStatus.Revoked => true,
            _ => false,
        };
        return valid ? null : "authority_record_state_invalid";
    }

    public static bool IsSafeIdentifier(string? value)
    {
        return value is not null && SafeIdentifier().IsMatch(value);
    }

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex SafeIdentifier();
}

// Thời điểm trong bằng chứng authority lưu dạng ISO-8601 UTC độ chính xác millisecond để so khớp chính xác với SQLite.
public static class AuthorityTimestamp
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static DateTimeOffset Now()
    {
        return Truncate(DateTimeOffset.UtcNow);
    }

    public static DateTimeOffset Truncate(DateTimeOffset value)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
    }

    public static bool AreEqual(DateTimeOffset? left, DateTimeOffset? right)
    {
        return left is not null && right is not null && left.Value.ToUnixTimeMilliseconds() == right.Value.ToUnixTimeMilliseconds();
    }

    public static string Serialize(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? value, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }
}
