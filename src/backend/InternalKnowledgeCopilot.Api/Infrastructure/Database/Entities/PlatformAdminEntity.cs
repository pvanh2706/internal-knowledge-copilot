using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

// Platform identity độc lập với tenant (DEC-0008 S01-Q01=B): không có TenantId, không liên kết bảng users,
// nên không bị tenant query filter áp vào. Tài khoản không bị xóa cứng; vô hiệu hóa bằng Status.
public sealed class PlatformAdminEntity
{
    public Guid Id { get; set; }

    public required string EmailNormalized { get; set; }

    public required string DisplayName { get; set; }

    // Hợp đồng lưu trữ có version theo định dạng PHC: $<thuật toán>$v=<version>$<tham số>$<salt>$<hash>.
    // Thuật toán và tham số cụ thể do S01-B quyết định; DB chỉ từ chối giá trị không theo định dạng này.
    public required string PasswordHash { get; set; }

    public PlatformAdminStatus Status { get; set; } = PlatformAdminStatus.Active;

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public bool MustChangePassword { get; set; } = true;

    public int TokenVersion { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
