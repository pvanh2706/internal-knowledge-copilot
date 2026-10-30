using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

public static class ProvisioningAuthorityServiceCollectionExtensions
{
    // API chỉ đăng ký reader (read-only); quyền ghi authority (reserve/activate/recovery) chỉ tồn tại trong CLI.
    public static IServiceCollection AddPlatformProvisioningVerifier(this IServiceCollection services, ProvisioningAuthoritySettings? settings)
    {
        if (settings is null)
        {
            services.AddSingleton<IPlatformProvisioningVerifier, UnverifiedPlatformProvisioningVerifier>();
            return services;
        }

        services.AddSingleton(settings);
        services.AddSingleton<IProvisioningAuthorityReader>(_ => settings.Kind switch
        {
            ProvisioningAuthorityKind.LocalSeal => new LocalSealProvisioningAuthorityReader(settings),
            ProvisioningAuthorityKind.DynamoDb => new DynamoDbProvisioningAuthorityReader(DynamoDbAuthorityClientFactory.CreateRuntimeClient(settings), settings),
            _ => throw new InvalidOperationException("provisioning_authority_kind_unsupported"),
        });
        services.AddSingleton<IPlatformProvisioningVerifier, ProvisioningAuthorityVerifier>();
        return services;
    }
}
