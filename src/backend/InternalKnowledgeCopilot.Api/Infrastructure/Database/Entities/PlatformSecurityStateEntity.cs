using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

// Trạng thái bootstrap một lần (DEC-0008 S01-Q04=A). DB ép singleton (id = 1), cấm xóa, cấm đảo ngược trạng thái
// Completed và buộc StateVersion tăng đúng 1 mỗi lần cập nhật. Migration không tạo bản ghi nào; S01-C tạo
// bản ghi cùng tài khoản đầu tiên và audit trong một transaction.
public sealed class PlatformSecurityStateEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public Guid DeploymentId { get; set; }

    public PlatformBootstrapStatus BootstrapStatus { get; set; } = PlatformBootstrapStatus.NotStarted;

    public Guid? BootstrapAdminId { get; set; }

    public PlatformAdminEntity? BootstrapAdmin { get; set; }

    public DateTimeOffset? BootstrapCompletedAt { get; set; }

    // Concurrency token do ứng dụng quản lý (SQLite không có rowversion).
    public int StateVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
