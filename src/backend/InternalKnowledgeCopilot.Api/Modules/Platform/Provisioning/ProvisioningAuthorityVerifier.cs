using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

// Verifier runtime của S01-C cho PlatformProvisioningGate: mỗi lần kiểm tra đọc lại authority (strongly consistent với
// DynamoDB), không cache, chỉ đọc. Lý do từ chối chỉ là mã ngắn, không chứa dữ liệu bản ghi.
public sealed class ProvisioningAuthorityVerifier(
    IProvisioningAuthorityReader reader,
    ProvisioningAuthoritySettings settings,
    ILogger<ProvisioningAuthorityVerifier> logger) : IPlatformProvisioningVerifier
{
    public async Task<bool> IsProvisioningVerifiedAsync(PlatformSecurityStateEntity state, CancellationToken cancellationToken = default)
    {
        var read = await reader.ReadAsync(cancellationToken);
        var result = ProvisioningEvidence.EvaluateRuntime(state, read, settings.Identity);
        if (!result.Verified)
        {
            logger.LogWarning("Platform provisioning evidence not verified ({Provider}): {Reason}", settings.ProviderCode, result.Reason);
        }

        return result.Verified;
    }
}
