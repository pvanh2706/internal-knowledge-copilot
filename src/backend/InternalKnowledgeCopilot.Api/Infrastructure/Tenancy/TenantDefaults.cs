namespace InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

public static class TenantDefaults
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string DefaultTenantCode = "default";

    public const string DefaultApplicationCode = "internal-knowledge-copilot";

    public const string DefaultLocalKnowledgeSourceExternalId = "local";
}
