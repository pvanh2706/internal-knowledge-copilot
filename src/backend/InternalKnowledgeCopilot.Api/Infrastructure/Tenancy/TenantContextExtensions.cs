namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public static class TenantContextExtensions
{
    public static Guid GetRequiredTenantId(this ITenantContext tenantContext)
    {
        return tenantContext.TenantId
            ?? throw new InvalidOperationException("tenant_required");
    }
}
