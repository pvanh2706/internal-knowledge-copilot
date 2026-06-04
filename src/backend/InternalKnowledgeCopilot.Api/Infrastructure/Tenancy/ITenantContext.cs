namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? TenantCode { get; }

    Guid? ApplicationId { get; }

    bool HasTenant { get; }

    void SetTenant(Guid tenantId, string tenantCode);

    void SetApplication(Guid? applicationId);
}
