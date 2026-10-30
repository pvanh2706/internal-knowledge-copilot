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

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LoginSucceeded, LoginFailed, LoginDenied, Lockout, PasswordChanged, PasswordChangeFailed, TokensRevoked, SessionRejected, AccessDenied,
    };
}

public static class PlatformAuditMetadataKeys
{
    public const string Reason = "reason";
    public const string SessionType = "session_type";
    public const string FailedLoginCount = "failed_login_count";
    public const string LockoutMinutes = "lockout_minutes";
    public const string TokenVersion = "token_version";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Reason, SessionType, FailedLoginCount, LockoutMinutes, TokenVersion,
    };
}

public sealed record PlatformAuditEntry(
    string Action,
    PlatformAuditOutcome Outcome,
    Guid? ActorPlatformId,
    string? RequestId,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IPlatformAuditWriter
{
    // Chỉ đưa bản ghi vào DbContext; caller SaveChanges cùng transaction với thay đổi bảo mật để audit và mutation
    // cùng commit hoặc cùng rollback.
    void Stage(PlatformAuditEntry entry);
}

// Audit platform: không bao giờ gán tenant (TargetTenantId luôn null ở S01-B), chỉ nhận action và metadata key trong
// allowlist, giá trị metadata là mã ngắn nên không thể chứa mật khẩu, hash, token hay payload.
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
            TargetType = entry.ActorPlatformId is null ? null : "PlatformAdmin",
            TargetId = entry.ActorPlatformId,
            Outcome = entry.Outcome,
            RequestId = string.IsNullOrWhiteSpace(entry.RequestId) ? null : entry.RequestId[..Math.Min(entry.RequestId.Length, MaxRequestIdLength)],
            MetadataJson = metadataJson,
            OccurredAt = DateTimeOffset.UtcNow,
        });
    }

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex SafeMetadataValue();
}
