using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.PlatformCli.TestHarness;

// Giả lập provisioning authority ĐỘC LẬP cho LocalSeal (Development/Testing). Production không có tương đương trong
// codebase: Authorized/RecoveryPending/Revoked do quy trình provisioning bên ngoài (C-EX2) tạo trên DynamoDB.
public sealed class LocalSealAuthoritySimulator(ProvisioningAuthoritySettings settings)
{
    public ProvisioningAuthorityRecord? Current()
    {
        return LocalSealFile.Read(settings.LocalSealPath!, settings.LocalSealIntegrityKey!).Record;
    }

    // Cấp quyền bootstrap mới. Ghi đè Reserved chỉ là quyết định tường minh của authority (không phải hết hạn tự động).
    public ProvisioningAuthorityRecord Authorize()
    {
        var previous = Current();
        return Write(new ProvisioningAuthorityRecord
        {
            DeploymentId = settings.Identity.DeploymentId,
            AuthorityId = settings.Identity.AuthorityId,
            Status = ProvisioningAuthorityStatus.Authorized,
            Version = (previous?.Version ?? 0) + 1,
            AuthorizationId = $"auth-{Guid.NewGuid():N}",
            AuthorizedAt = AuthorityTimestamp.Now(),
        });
    }

    public ProvisioningAuthorityRecord AuthorizeAdminRecovery(Guid targetAdminId)
    {
        var current = Current() ?? throw new InvalidOperationException("authority_record_missing");
        return Write(current with
        {
            Status = ProvisioningAuthorityStatus.RecoveryPending,
            Version = current.Version + 1,
            RecoveryId = Guid.NewGuid(),
            RecoveryPurpose = ProvisioningRecoveryPurpose.AdminRecovery,
            RecoveryTargetAdminId = targetAdminId,
            RecoveryAuthorizedAt = AuthorityTimestamp.Now(),
        });
    }

    public ProvisioningAuthorityRecord AuthorizeBootstrapReconciliation()
    {
        var current = Current() ?? throw new InvalidOperationException("authority_record_missing");
        return Write(current with
        {
            Status = ProvisioningAuthorityStatus.RecoveryPending,
            Version = current.Version + 1,
            RecoveryId = Guid.NewGuid(),
            RecoveryPurpose = ProvisioningRecoveryPurpose.BootstrapReconciliation,
            RecoveryAuthorizedAt = AuthorityTimestamp.Now(),
        });
    }

    public ProvisioningAuthorityRecord Revoke()
    {
        var current = Current() ?? throw new InvalidOperationException("authority_record_missing");
        return Write(current with { Status = ProvisioningAuthorityStatus.Revoked, Version = current.Version + 1 });
    }

    public ProvisioningAuthorityRecord Write(ProvisioningAuthorityRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.LocalSealPath!)!);
        LocalSealFile.Write(settings.LocalSealPath!, settings.LocalSealIntegrityKey!, record);
        return record;
    }
}
