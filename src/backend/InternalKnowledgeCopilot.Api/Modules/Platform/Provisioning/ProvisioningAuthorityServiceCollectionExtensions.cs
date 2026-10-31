using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

public static class ProvisioningAuthorityServiceCollectionExtensions
{
    // API chỉ đăng ký reader (read-only). Thay đổi authority chỉ do Authority Service phía server thực hiện (S01-C-REM);
    // CLI chỉ gửi yêu cầu tới service, API không có cả hai.
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
