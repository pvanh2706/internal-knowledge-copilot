using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.PlatformCli.Preflight;

public sealed record PlatformAdminSummary(
    Guid Id,
    string Email,
    PlatformAdminStatus Status,
    bool MustChangePassword,
    DateTimeOffset? LockedUntil,
    int TokenVersion);

// Ảnh chụp chỉ-đọc trạng thái platform trong SQLite. SchemaProblem khác null thì không đọc bảng platform nào nữa.
public sealed record PlatformDatabaseInspection(
    string? SchemaProblem,
    PlatformSecurityStateEntity? State,
    IReadOnlyList<PlatformAdminSummary> Admins,
    long AuditCount,
    IReadOnlyList<Guid?> BootstrapAuditReservationIds,
    IReadOnlySet<Guid> AppliedRecoveryIds)
{
    public static PlatformDatabaseInspection SchemaInvalid(string problem) => new(problem, null, [], 0, [], new HashSet<Guid>());

    public bool IsPristine => SchemaProblem is null && State is null && Admins.Count == 0 && AuditCount == 0;

    public bool IsCompleted => SchemaProblem is null && State is { BootstrapStatus: PlatformBootstrapStatus.Completed };

    public Guid? BootstrapAuditReservationId => BootstrapAuditReservationIds.Count == 1 ? BootstrapAuditReservationIds[0] : null;

    // Trạng thái SQLite không thể sinh ra bởi luồng hợp lệ (S01-B không ghi audit trước bootstrap; S01-C luôn tạo
    // admin + state + audit trong cùng transaction).
    public string? Anomaly
    {
        get
        {
            if (SchemaProblem is not null)
            {
                return SchemaProblem;
            }

            if (State is { BootstrapStatus: not PlatformBootstrapStatus.Completed })
            {
                return "security_state_incompatible";
            }

            if (State is null && Admins.Count > 0)
            {
                return "platform_admin_without_bootstrap_state";
            }

            if (State is null && AuditCount > 0)
            {
                return "platform_audit_without_bootstrap_state";
            }

            if (State is not null && (BootstrapAuditReservationIds.Count != 1 || BootstrapAuditReservationIds[0] is null))
            {
                return "bootstrap_audit_evidence_invalid";
            }

            if (State is not null && Admins.All(admin => admin.Id != State.BootstrapAdminId))
            {
                return "bootstrap_admin_missing";
            }

            return null;
        }
    }

    // Điều kiện bootstrap lần đầu (C2): schema đúng, chưa có state, không có admin, không có lịch sử audit platform.
    public string? BootstrapIneligibility => SchemaProblem
        ?? (State is { BootstrapStatus: PlatformBootstrapStatus.Completed } ? "bootstrap_already_completed"
            : State is not null ? "security_state_incompatible"
            : Admins.Count > 0 ? "platform_admin_exists"
            : AuditCount > 0 ? "platform_audit_history_present"
            : null);
}

public static class PlatformDatabaseInspector
{
    private static readonly string[] PlatformTables = ["platform_admins", "platform_audit_logs", "platform_security_state"];

    // C-IR-09: nội dung trigger S01-A (migration 20261030070953_AddPlatformDatabaseFoundation) sau khi chuẩn hóa khoảng trắng.
    // Trigger cùng tên nhưng thân khác (ví dụ no-op) hoặc trigger lạ gắn vào bảng platform đều làm preflight thất bại.
    private static readonly IReadOnlyDictionary<string, string> ExpectedTriggerSql = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["trg_platform_admins_no_delete"] =
            "CREATE TRIGGER \"trg_platform_admins_no_delete\" BEFORE DELETE ON \"platform_admins\" BEGIN SELECT RAISE(ABORT, 'platform_admins_delete_forbidden'); END",
        ["trg_platform_audit_logs_no_update"] =
            "CREATE TRIGGER \"trg_platform_audit_logs_no_update\" BEFORE UPDATE ON \"platform_audit_logs\" BEGIN SELECT RAISE(ABORT, 'platform_audit_logs_append_only'); END",
        ["trg_platform_audit_logs_no_delete"] =
            "CREATE TRIGGER \"trg_platform_audit_logs_no_delete\" BEFORE DELETE ON \"platform_audit_logs\" BEGIN SELECT RAISE(ABORT, 'platform_audit_logs_append_only'); END",
        ["trg_platform_security_state_no_delete"] =
            "CREATE TRIGGER \"trg_platform_security_state_no_delete\" BEFORE DELETE ON \"platform_security_state\" BEGIN SELECT RAISE(ABORT, 'platform_security_state_delete_forbidden'); END",
        ["trg_platform_security_state_guard_update"] =
            "CREATE TRIGGER \"trg_platform_security_state_guard_update\" BEFORE UPDATE ON \"platform_security_state\" BEGIN " +
            "SELECT RAISE(ABORT, 'platform_security_state_identity_immutable') WHERE NEW.\"id\" IS NOT OLD.\"id\" OR NEW.\"deployment_id\" IS NOT OLD.\"deployment_id\" OR NEW.\"created_at\" IS NOT OLD.\"created_at\"; " +
            "SELECT RAISE(ABORT, 'platform_bootstrap_completed_immutable') WHERE OLD.\"bootstrap_status\" = 'Completed' AND (NEW.\"bootstrap_status\" IS NOT OLD.\"bootstrap_status\" OR NEW.\"bootstrap_admin_id\" IS NOT OLD.\"bootstrap_admin_id\" OR NEW.\"bootstrap_completed_at\" IS NOT OLD.\"bootstrap_completed_at\"); " +
            "SELECT RAISE(ABORT, 'platform_security_state_version_conflict') WHERE NEW.\"state_version\" IS NOT OLD.\"state_version\" + 1; END",
    };

    // Không migrate, không tạo bảng: schema phải khớp chính xác migration mà CLI được build cùng.
    public static async Task<PlatformDatabaseInspection> InspectAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var schemaProblem = await CheckSchemaAsync(db, cancellationToken);
        if (schemaProblem is not null)
        {
            return PlatformDatabaseInspection.SchemaInvalid(schemaProblem);
        }

        var state = await db.PlatformSecurityState.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var admins = (await db.PlatformAdmins.AsNoTracking()
                .Select(admin => new PlatformAdminSummary(admin.Id, admin.EmailNormalized, admin.Status, admin.MustChangePassword, admin.LockedUntil, admin.TokenVersion))
                .ToListAsync(cancellationToken))
            .OrderBy(admin => admin.Email, StringComparer.Ordinal)
            .ToList();
        var auditCount = await db.PlatformAuditLogs.LongCountAsync(cancellationToken);
        var bootstrapAudits = await db.PlatformAuditLogs.AsNoTracking()
            .Where(log => log.Action == PlatformAuditActions.BootstrapCompleted && log.Outcome == PlatformAuditOutcome.Success)
            .Select(log => log.MetadataJson)
            .ToListAsync(cancellationToken);
        var recoveryActions = PlatformAuditActions.RecoveryApplied.ToList();
        var recoveryAudits = await db.PlatformAuditLogs.AsNoTracking()
            .Where(log => recoveryActions.Contains(log.Action) && log.Outcome == PlatformAuditOutcome.Success)
            .Select(log => log.MetadataJson)
            .ToListAsync(cancellationToken);

        return new PlatformDatabaseInspection(
            null,
            state,
            admins,
            auditCount,
            bootstrapAudits.Select(metadata => ReadGuid(metadata, PlatformAuditMetadataKeys.ReservationId)).ToList(),
            recoveryAudits.Select(metadata => ReadGuid(metadata, PlatformAuditMetadataKeys.RecoveryId)).OfType<Guid>().ToHashSet());
    }

    public static async Task<bool> IsRecoveryAppliedAsync(AppDbContext db, Guid recoveryId, CancellationToken cancellationToken = default)
    {
        var recoveryActions = PlatformAuditActions.RecoveryApplied.ToList();
        var audits = await db.PlatformAuditLogs.AsNoTracking()
            .Where(log => recoveryActions.Contains(log.Action) && log.Outcome == PlatformAuditOutcome.Success)
            .Select(log => log.MetadataJson)
            .ToListAsync(cancellationToken);
        return audits.Any(metadata => ReadGuid(metadata, PlatformAuditMetadataKeys.RecoveryId) == recoveryId);
    }

    private static async Task<string?> CheckSchemaAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        if (applied.Except(known).Any())
        {
            return "schema_newer_than_cli";
        }

        if (known.Except(applied).Any())
        {
            return "schema_not_current";
        }

        return await PlatformTriggersIntactAsync(db, cancellationToken) ? null : "schema_integrity_failed";
    }

    public static string NormalizeSql(string sql)
    {
        return string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd(';').TrimEnd();
    }

    private static async Task<bool> PlatformTriggersIntactAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name, tbl_name, sql FROM sqlite_master WHERE type = 'trigger'";
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var table = reader.GetString(1);
                if (PlatformTables.Contains(table, StringComparer.OrdinalIgnoreCase) || name.StartsWith("trg_platform_", StringComparison.OrdinalIgnoreCase))
                {
                    actual[name] = reader.IsDBNull(2) ? string.Empty : NormalizeSql(reader.GetString(2));
                }
            }
        }

        return actual.Count == ExpectedTriggerSql.Count
            && ExpectedTriggerSql.All(expected => actual.TryGetValue(expected.Key, out var sql) && string.Equals(sql, expected.Value, StringComparison.Ordinal));
    }

    private static Guid? ReadGuid(string? metadataJson, string key)
    {
        if (metadataJson is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String
                && Guid.TryParseExact(value.GetString(), "D", out var guid)
                    ? guid
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
