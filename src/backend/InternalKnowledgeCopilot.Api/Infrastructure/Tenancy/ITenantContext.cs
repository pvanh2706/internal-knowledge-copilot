namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? TenantCode { get; }

    bool HasTenant { get; }

    void SetTenant(Guid tenantId, string tenantCode);
}
