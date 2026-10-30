namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

// S01-C: nguồn bằng chứng provisioning nằm ngoài SQLite. Không có thuộc tính nào cho phép bỏ qua kiểm tra; Provider=None
// (mặc định) giữ hành vi S01-B: platform auth luôn bị từ chối.
public sealed class ProvisioningAuthorityOptions
{
    public const string SectionName = "ProvisioningAuthority";

    public string Provider { get; init; } = ProvisioningAuthorityProviders.None;

    public string? DeploymentId { get; init; }

    public string? AuthorityId { get; init; }

    public LocalSealAuthorityOptions LocalSeal { get; init; } = new();

    public DynamoDbAuthorityOptions DynamoDb { get; init; } = new();
}

public static class ProvisioningAuthorityProviders
{
    public const string None = "None";
    public const string LocalSeal = "LocalSeal";
    public const string DynamoDb = "DynamoDb";
}

// Chỉ dành cho Development/Testing. Seal nằm trên cùng máy nên bị rollback cùng một bản restore toàn máy.
public sealed class LocalSealAuthorityOptions
{
    public string? Path { get; init; }

    // Base64, tối thiểu 32 byte; dùng cho HMAC-SHA256 toàn vẹn của file seal.
    public string? IntegrityKey { get; init; }
}

// Credential AWS không bao giờ đọc từ appsettings: runtime dùng default credential chain (IAM role chỉ có GetItem),
// CLI dùng OperatorProfile (named profile của operator, quyền UpdateItem có điều kiện).
public sealed class DynamoDbAuthorityOptions
{
    public string? TableName { get; init; }

    public string? Region { get; init; }

    public int TimeoutSeconds { get; init; } = 3;

    public string? OperatorProfile { get; init; }
}
