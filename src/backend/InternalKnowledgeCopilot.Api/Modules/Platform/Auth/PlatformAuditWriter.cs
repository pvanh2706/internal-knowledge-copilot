using System.Text.Json;
using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

public static class PlatformAuditActions
{
    public const string LoginSucceeded = "platform.auth.login_succeeded";
    public const string LoginFailed = "platform.auth.login_failed";
    public const string LoginDenied = "platform.auth.login_denied";
    public const string Lockout = "platform.auth.lockout";
    public const string PasswordChanged = "platform.auth.password_changed";
    public const string PasswordChangeFailed = "platform.auth.password_change_failed";
    public const string TokensRevoked = "platform.auth.tokens_revoked";
    public const string SessionRejected = "platform.auth.session_rejected";
    public const string AccessDenied = "platform.auth.access_denied";

    // S01-C: chỉ CLI bootstrap/recovery ghi các action dưới đây (actor null vì operator không phải tài khoản platform; danh tính
    // operator nằm trong metadata operator_person_id; target là tài khoản platform bị tác động).
    public const string BootstrapCompleted = "platform.bootstrap.completed";
    public const string BootstrapDenied = "platform.bootstrap.denied";
    public const string AdminRecovered = "platform.recovery.admin_recovered";
    public const string BootstrapReconciled = "platform.recovery.bootstrap_reconciled";
    public const string RecoveryDenied = "platform.recovery.denied";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LoginSucceeded, LoginFailed, LoginDenied, Lockout, PasswordChanged, PasswordChangeFailed, TokensRevoked, SessionRejected, AccessDenied,
        BootstrapCompleted, BootstrapDenied, AdminRecovered, BootstrapReconciled, RecoveryDenied,
    };

    // Recovery thành công: metadata recovery_id của các action này là bằng chứng một recovery đã áp dụng vào SQLite.
    public static readonly IReadOnlySet<string> RecoveryApplied = new HashSet<string>(StringComparer.Ordinal)
    {
        AdminRecovered, BootstrapReconciled,
    };
}

public static class PlatformAuditMetadataKeys
{
    public const string Reason = "reason";
    public const string SessionType = "session_type";
    public const string FailedLoginCount = "failed_login_count";
    public const string LockoutMinutes = "lockout_minutes";
    public const string TokenVersion = "token_version";

    // S01-C: chỉ định danh (GUID) và mã ngắn, không chứa email, mật khẩu, hash hay credential của authority.
    public const string DeploymentId = "deployment_id";
    public const string ReservationId = "reservation_id";
    public const string RecoveryId = "recovery_id";
    public const string RecoveryPurpose = "recovery_purpose";
    public const string StateVersion = "state_version";
    public const string AuthorityProvider = "authority_provider";

    // S01-C-REM (C-IR-04): danh tính người thực hiện bootstrap/recovery. person_id do Authority Service xác minh (DynamoDb) hoặc
    // nhãn chưa xác minh của LocalSeal; identity_assurance cho biết mức đảm bảo. Không chứa email, tên tài khoản OS hay ARN.
    public const string OperatorPersonId = "operator_person_id";
    public const string ApproverPersonId = "approver_person_id";
    public const string IdentityAssurance = "identity_assurance";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Reason, SessionType, FailedLoginCount, LockoutMinutes, TokenVersion,
        DeploymentId, ReservationId, RecoveryId, RecoveryPurpose, StateVersion, AuthorityProvider,
        OperatorPersonId, ApproverPersonId, IdentityAssurance,
    };
}

public sealed record PlatformAuditEntry(
    string Action,
    PlatformAuditOutcome Outcome,
    Guid? ActorPlatformId,
    string? RequestId,
    IReadOnlyDictionary<string, string>? Metadata = null,
    Guid? TargetPlatformAdminId = null);

public interface IPlatformAuditWriter
{
    // Chỉ đưa bản ghi vào DbContext; caller SaveChanges cùng transaction với thay đổi bảo mật để audit và mutation
    // cùng commit hoặc cùng rollback.
    void Stage(PlatformAuditEntry entry);
}

// Audit platform: không bao giờ gán tenant (TargetTenantId luôn null ở S01-B/C), chỉ nhận action và metadata key trong
// allowlist, giá trị metadata là mã ngắn nên không thể chứa mật khẩu, hash, token hay payload. Target mặc định là actor;
// sự kiện CLI (không có actor) chỉ định tài khoản bị tác động qua TargetPlatformAdminId.
public sealed partial class PlatformAuditWriter(AppDbContext dbContext) : IPlatformAuditWriter
{
    private const int MaxRequestIdLength = 100;

    public void Stage(PlatformAuditEntry entry)
    {
        if (!PlatformAuditActions.All.Contains(entry.Action))
        {
            throw new ArgumentException("platform_audit_action_not_allowed");
        }

        string? metadataJson = null;
        if (entry.Metadata is { Count: > 0 })
        {
            foreach (var (key, value) in entry.Metadata)
            {
                if (!PlatformAuditMetadataKeys.All.Contains(key) || !SafeMetadataValue().IsMatch(value))
                {
                    throw new ArgumentException("platform_audit_metadata_not_allowed");
                }
            }

            metadataJson = JsonSerializer.Serialize(new SortedDictionary<string, string>(entry.Metadata.ToDictionary(), StringComparer.Ordinal));
        }

        dbContext.PlatformAuditLogs.Add(new PlatformAuditLogEntity
        {
            Id = Guid.NewGuid(),
            ActorPlatformId = entry.ActorPlatformId,
            Action = entry.Action,
            TargetTenantId = null,
            TargetType = (entry.TargetPlatformAdminId ?? entry.ActorPlatformId) is null ? null : "PlatformAdmin",
            TargetId = entry.TargetPlatformAdminId ?? entry.ActorPlatformId,
            Outcome = entry.Outcome,
            RequestId = string.IsNullOrWhiteSpace(entry.RequestId) ? null : entry.RequestId[..Math.Min(entry.RequestId.Length, MaxRequestIdLength)],
            MetadataJson = metadataJson,
            OccurredAt = DateTimeOffset.UtcNow,
        });
    }

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex SafeMetadataValue();
}
