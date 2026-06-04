namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public string? TenantCode { get; private set; }

    public bool HasTenant => TenantId is not null;

    public void SetTenant(Guid tenantId, string tenantCode)
    {
        TenantId = tenantId;
        TenantCode = tenantCode;
    }
}
