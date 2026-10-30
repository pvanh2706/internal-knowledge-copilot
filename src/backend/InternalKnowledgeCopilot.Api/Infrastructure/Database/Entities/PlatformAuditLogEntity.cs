using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

// Audit platform tách khỏi audit_logs của tenant và không fallback về default tenant: TargetTenantId chỉ có giá trị
// khi sự kiện thực sự nhắm vào một tenant. Bảng append-only (trigger chặn UPDATE/DELETE).
// MetadataJson không được chứa mật khẩu, token, signing key hay secret; writer ở S01-B/C phải lọc trước khi ghi.
public sealed class PlatformAuditLogEntity
{
    public Guid Id { get; set; }

    public Guid? ActorPlatformId { get; set; }

    public PlatformAdminEntity? ActorPlatformAdmin { get; set; }

    public required string Action { get; set; }

    public Guid? TargetTenantId { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public PlatformAuditOutcome Outcome { get; set; }

    public string? RequestId { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
