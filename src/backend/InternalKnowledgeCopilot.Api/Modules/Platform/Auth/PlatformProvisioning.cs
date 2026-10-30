using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

// Hợp đồng xác minh bằng chứng provisioning nằm ngoài DB (Baseline v1.0 §7). Trạng thái Completed trong DB
// một mình không đủ, vì bản restore DB có thể làm giả nó. S01-C sẽ cung cấp implementation thật.
public interface IPlatformProvisioningVerifier
{
    Task<bool> IsProvisioningVerifiedAsync(PlatformSecurityStateEntity state, CancellationToken cancellationToken = default);
}

// Mặc định an toàn cho mọi môi trường: không xác minh được nên luôn từ chối. Không có cấu hình nào bật bypass;
// chỉ test mới thay implementation qua DI.
public sealed class UnverifiedPlatformProvisioningVerifier : IPlatformProvisioningVerifier
{
    public Task<bool> IsProvisioningVerifiedAsync(PlatformSecurityStateEntity state, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}

// BootstrapCompleted: bản ghi state tồn tại và Completed. Khi false (trước bootstrap hoặc không đọc được state) không được
// ghi platform_audit_logs: S01-C coi "có admin/audit nhưng state chưa Completed" là bất thường và fail closed, nên một lần
// đăng nhập thử trước bootstrap không được làm mất điều kiện bootstrap lần đầu.
public sealed record PlatformProvisioningStatus(bool IsReady, string Reason, bool BootstrapCompleted)
{
    public static readonly PlatformProvisioningStatus Ready = new(true, "ready", true);
}

public interface IPlatformProvisioningGate
{
    Task<PlatformProvisioningStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class PlatformProvisioningGate(AppDbContext dbContext, IPlatformProvisioningVerifier verifier) : IPlatformProvisioningGate
{
    public async Task<PlatformProvisioningStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await dbContext.PlatformSecurityState
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == PlatformSecurityStateEntity.SingletonId, cancellationToken);
            if (state is null)
            {
                return new PlatformProvisioningStatus(false, "bootstrap_state_missing", BootstrapCompleted: false);
            }

            if (state.BootstrapStatus != PlatformBootstrapStatus.Completed || state.BootstrapAdminId is null || state.BootstrapCompletedAt is null)
            {
                return new PlatformProvisioningStatus(false, "bootstrap_not_completed", BootstrapCompleted: false);
            }

            return await verifier.IsProvisioningVerifiedAsync(state, cancellationToken)
                ? PlatformProvisioningStatus.Ready
                : new PlatformProvisioningStatus(false, "provisioning_unverified", BootstrapCompleted: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PlatformProvisioningStatus(false, "provisioning_check_failed", BootstrapCompleted: false);
        }
    }
}
