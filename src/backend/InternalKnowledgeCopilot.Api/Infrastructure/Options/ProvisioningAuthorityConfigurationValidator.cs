using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

// Chặn khởi động (API và CLI) khi cấu hình provisioning authority thiếu hoặc không an toàn. Không có fallback:
// provider không hợp lệ không bao giờ bị hạ xuống None hay LocalSeal.
public static partial class ProvisioningAuthorityConfigurationValidator
{
    public const int MinIntegrityKeyBytes = 32;
    public const int MinDynamoDbTimeoutSeconds = 1;
    public const int MaxDynamoDbTimeoutSeconds = 30;

    private static readonly string[] LocalSealEnvironments = ["Development", "Testing"];

    // Trả null khi Provider=None (platform auth luôn bị từ chối như S01-B).
    public static ProvisioningAuthoritySettings? Validate(ProvisioningAuthorityOptions options, string environmentName, string sqlitePath)
    {
        var provider = options.Provider?.Trim();
        if (string.IsNullOrEmpty(provider) || string.Equals(provider, ProvisioningAuthorityProviders.None, StringComparison.Ordinal))
        {
            return null;
        }

        if (!Guid.TryParse(options.DeploymentId, out var deploymentId) || deploymentId == Guid.Empty)
        {
            throw new InvalidOperationException("ProvisioningAuthority:DeploymentId phải là GUID khác rỗng.");
        }

        if (!ProvisioningAuthorityRecord.IsSafeIdentifier(options.AuthorityId))
        {
            throw new InvalidOperationException("ProvisioningAuthority:AuthorityId phải gồm 1-64 ký tự [A-Za-z0-9_.:-].");
        }

        var identity = new ProvisioningAuthorityIdentity(deploymentId, options.AuthorityId!);
        return provider switch
        {
            ProvisioningAuthorityProviders.LocalSeal => ValidateLocalSeal(options.LocalSeal, identity, environmentName, sqlitePath),
            ProvisioningAuthorityProviders.DynamoDb => ValidateDynamoDb(options.DynamoDb, options.AuthorityService, identity),
            _ => throw new InvalidOperationException("ProvisioningAuthority:Provider không được hỗ trợ (None, LocalSeal, DynamoDb)."),
        };
    }

    public static bool IsLocalSealEnvironment(string environmentName)
    {
        return LocalSealEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
    }

    private static ProvisioningAuthoritySettings ValidateLocalSeal(
        LocalSealAuthorityOptions options,
        ProvisioningAuthorityIdentity identity,
        string environmentName,
        string sqlitePath)
    {
        // Local seal không bao giờ là fallback production.
        if (!IsLocalSealEnvironment(environmentName))
        {
            throw new InvalidOperationException("ProvisioningAuthority:Provider=LocalSeal chỉ được dùng trong môi trường Development hoặc Testing.");
        }

        if (string.IsNullOrWhiteSpace(options.Path))
        {
            throw new InvalidOperationException("ProvisioningAuthority:LocalSeal:Path chưa được cấu hình.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.IntegrityKey ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("ProvisioningAuthority:LocalSeal:IntegrityKey phải là base64.");
        }

        if (key.Length < MinIntegrityKeyBytes)
        {
            throw new InvalidOperationException("ProvisioningAuthority:LocalSeal:IntegrityKey cần tối thiểu 32 byte.");
        }

        // Seal nằm trong thư mục DB sẽ bị restore cùng DB, mất ý nghĩa bằng chứng độc lập.
        var sealPath = Path.GetFullPath(options.Path);
        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!;
        if (IsWithin(sealPath, databaseDirectory))
        {
            throw new InvalidOperationException("ProvisioningAuthority:LocalSeal:Path không được nằm trong thư mục chứa SQLite database.");
        }

        return new ProvisioningAuthoritySettings(ProvisioningAuthorityKind.LocalSeal, identity, LocalSealPath: sealPath, LocalSealIntegrityKey: key);
    }

    private static ProvisioningAuthoritySettings ValidateDynamoDb(DynamoDbAuthorityOptions options, AuthorityServiceOptions service, ProvisioningAuthorityIdentity identity)
    {
        Uri? serviceEndpoint = null;
        if (!string.IsNullOrWhiteSpace(service.Endpoint)
            && (!Uri.TryCreate(service.Endpoint.Trim(), UriKind.Absolute, out serviceEndpoint) || serviceEndpoint.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(serviceEndpoint.Query) || !string.IsNullOrEmpty(serviceEndpoint.UserInfo)))
        {
            throw new InvalidOperationException("ProvisioningAuthority:AuthorityService:Endpoint phải là URL HTTPS tuyệt đối, không có query hay thông tin đăng nhập.");
        }

        if (string.IsNullOrWhiteSpace(options.TableName) || options.TableName.Length is < 3 or > 255)
        {
            throw new InvalidOperationException("ProvisioningAuthority:DynamoDb:TableName chưa được cấu hình hợp lệ.");
        }

        if (options.Region is null || !AwsRegionFormat().IsMatch(options.Region.Trim()))
        {
            throw new InvalidOperationException("ProvisioningAuthority:DynamoDb:Region phải là AWS region hợp lệ (cấu hình tường minh, không suy luận).");
        }

        if (options.TimeoutSeconds is < MinDynamoDbTimeoutSeconds or > MaxDynamoDbTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"ProvisioningAuthority:DynamoDb:TimeoutSeconds phải nằm trong khoảng {MinDynamoDbTimeoutSeconds}-{MaxDynamoDbTimeoutSeconds}.");
        }

        return new ProvisioningAuthoritySettings(
            ProvisioningAuthorityKind.DynamoDb,
            identity,
            DynamoDbTableName: options.TableName.Trim(),
            DynamoDbRegion: options.Region.Trim(),
            DynamoDbTimeout: TimeSpan.FromSeconds(options.TimeoutSeconds),
            DynamoDbOperatorProfile: string.IsNullOrWhiteSpace(options.OperatorProfile) ? null : options.OperatorProfile.Trim(),
            AuthorityServiceEndpoint: serviceEndpoint);
    }

    [GeneratedRegex("^[a-z]{2}(-[a-z]+)+-[0-9]{1,2}$")]
    private static partial Regex AwsRegionFormat();

    private static bool IsWithin(string path, string directory)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(normalizedDirectory, comparison);
    }
}
