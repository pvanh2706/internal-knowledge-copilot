using System.Globalization;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

// Một item cho mỗi deployment, partition key "pk" = "deployment#<deployment_id>". Thuộc tính ngoài danh sách dưới đây
// được bỏ qua (authority độc lập có thể ghi thêm thông tin truy vết), nhưng mọi thuộc tính đã biết được kiểm tra chặt.
public static class DynamoDbAuthorityItem
{
    public const string PartitionKey = "pk";
    public const string RecordTypeValue = "platform_provisioning_authority";

    public const string RecordType = "record_type";
    public const string SchemaVersion = "schema_version";
    public const string DeploymentId = "deployment_id";
    public const string AuthorityId = "authority_id";
    public const string Status = "status";
    public const string Version = "version";
    public const string AuthorizationId = "authorization_id";
    public const string AuthorizedAt = "authorized_at";
    public const string ReservationId = "reservation_id";
    public const string ReservedAdminId = "reserved_admin_id";
    public const string ReservedAt = "reserved_at";
    public const string BootstrapAdminId = "bootstrap_admin_id";
    public const string BootstrapCompletedAt = "bootstrap_completed_at";
    public const string ActivatedAt = "activated_at";
    public const string StateVersion = "state_version";
    public const string RecoveryId = "recovery_id";
    public const string RecoveryPurpose = "recovery_purpose";
    public const string RecoveryTargetAdminId = "recovery_target_admin_id";
    public const string RecoveryAuthorizedAt = "recovery_authorized_at";
    public const string LastRecoveryId = "last_recovery_id";
    public const string LastRecoveredAt = "last_recovered_at";
    public const string RecoveryCount = "recovery_count";

    // Các thuộc tính có thể vắng mặt tùy trạng thái; khi ghi, thuộc tính null được REMOVE.
    public static readonly IReadOnlyList<string> OptionalAttributes =
    [
        ReservationId, ReservedAdminId, ReservedAt, BootstrapAdminId, BootstrapCompletedAt, ActivatedAt, StateVersion,
        RecoveryId, RecoveryPurpose, RecoveryTargetAdminId, RecoveryAuthorizedAt, LastRecoveryId, LastRecoveredAt,
    ];

    public static string PartitionKeyValue(Guid deploymentId) => $"deployment#{deploymentId:D}";

    public static Dictionary<string, AttributeValue> Key(Guid deploymentId)
    {
        return new Dictionary<string, AttributeValue> { [PartitionKey] = new() { S = PartitionKeyValue(deploymentId) } };
    }

    // Thuộc tính không phải khóa của bản ghi; trường null không xuất hiện.
    public static Dictionary<string, AttributeValue> ToAttributes(ProvisioningAuthorityRecord record)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [RecordType] = S(RecordTypeValue),
            [SchemaVersion] = N(ProvisioningAuthorityRecord.CurrentSchemaVersion),
            [DeploymentId] = S(record.DeploymentId.ToString("D")),
            [AuthorityId] = S(record.AuthorityId),
            [Status] = S(record.Status.ToString()),
            [Version] = N(record.Version),
            [AuthorizationId] = S(record.AuthorizationId),
            [AuthorizedAt] = S(AuthorityTimestamp.Serialize(record.AuthorizedAt)),
            [RecoveryCount] = N(record.RecoveryCount),
        };
        AddGuid(values, ReservationId, record.ReservationId);
        AddGuid(values, ReservedAdminId, record.ReservedAdminId);
        AddTime(values, ReservedAt, record.ReservedAt);
        AddGuid(values, BootstrapAdminId, record.BootstrapAdminId);
        AddTime(values, BootstrapCompletedAt, record.BootstrapCompletedAt);
        AddTime(values, ActivatedAt, record.ActivatedAt);
        if (record.StateVersion is { } stateVersion)
        {
            values[StateVersion] = N(stateVersion);
        }

        AddGuid(values, RecoveryId, record.RecoveryId);
        if (record.RecoveryPurpose is { } purpose)
        {
            values[RecoveryPurpose] = S(purpose.ToString());
        }

        AddGuid(values, RecoveryTargetAdminId, record.RecoveryTargetAdminId);
        AddTime(values, RecoveryAuthorizedAt, record.RecoveryAuthorizedAt);
        AddGuid(values, LastRecoveryId, record.LastRecoveryId);
        AddTime(values, LastRecoveredAt, record.LastRecoveredAt);
        return values;
    }

    public static ProvisioningAuthorityReadResult Parse(Dictionary<string, AttributeValue>? item, Guid expectedDeploymentId)
    {
        if (item is null || item.Count == 0)
        {
            return ProvisioningAuthorityReadResult.NotFound();
        }

        try
        {
            if (Text(item, PartitionKey) != PartitionKeyValue(expectedDeploymentId)
                || Text(item, RecordType) != RecordTypeValue
                || Number(item, SchemaVersion) != ProvisioningAuthorityRecord.CurrentSchemaVersion)
            {
                return ProvisioningAuthorityReadResult.Malformed("authority_record_type_invalid");
            }

            var record = new ProvisioningAuthorityRecord
            {
                DeploymentId = RequiredGuid(item, DeploymentId),
                AuthorityId = Text(item, AuthorityId) ?? throw new FormatException(),
                Status = ParseEnum<ProvisioningAuthorityStatus>(Text(item, Status)) ?? throw new FormatException(),
                Version = Number(item, Version) ?? throw new FormatException(),
                AuthorizationId = Text(item, AuthorizationId) ?? throw new FormatException(),
                AuthorizedAt = OptionalTime(item, AuthorizedAt) ?? throw new FormatException(),
                ReservationId = OptionalGuid(item, ReservationId),
                ReservedAdminId = OptionalGuid(item, ReservedAdminId),
                ReservedAt = OptionalTime(item, ReservedAt),
                BootstrapAdminId = OptionalGuid(item, BootstrapAdminId),
                BootstrapCompletedAt = OptionalTime(item, BootstrapCompletedAt),
                ActivatedAt = OptionalTime(item, ActivatedAt),
                StateVersion = Number(item, StateVersion) is { } stateVersion ? checked((int)stateVersion) : null,
                RecoveryId = OptionalGuid(item, RecoveryId),
                RecoveryPurpose = Text(item, RecoveryPurpose) is { } purpose
                    ? ParseEnum<ProvisioningRecoveryPurpose>(purpose) ?? throw new FormatException()
                    : null,
                RecoveryTargetAdminId = OptionalGuid(item, RecoveryTargetAdminId),
                RecoveryAuthorizedAt = OptionalTime(item, RecoveryAuthorizedAt),
                LastRecoveryId = OptionalGuid(item, LastRecoveryId),
                LastRecoveredAt = OptionalTime(item, LastRecoveredAt),
                RecoveryCount = checked((int)(Number(item, RecoveryCount) ?? throw new FormatException())),
            };
            if (record.DeploymentId != expectedDeploymentId)
            {
                return ProvisioningAuthorityReadResult.Malformed("authority_record_key_mismatch");
            }

            return record.Validate() is { } invalid
                ? ProvisioningAuthorityReadResult.Malformed(invalid)
                : ProvisioningAuthorityReadResult.Found(record);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return ProvisioningAuthorityReadResult.Malformed("authority_record_malformed");
        }
    }

    private static AttributeValue S(string value) => new() { S = value };

    private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static void AddGuid(Dictionary<string, AttributeValue> values, string name, Guid? value)
    {
        if (value is { } guid)
        {
            values[name] = S(guid.ToString("D"));
        }
    }

    private static void AddTime(Dictionary<string, AttributeValue> values, string name, DateTimeOffset? value)
    {
        if (value is { } time)
        {
            values[name] = S(AuthorityTimestamp.Serialize(time));
        }
    }

    // Thuộc tính tồn tại nhưng sai kiểu (không phải S) là malformed, không bị coi như vắng mặt.
    private static string? Text(Dictionary<string, AttributeValue> item, string name)
    {
        if (!item.TryGetValue(name, out var value))
        {
            return null;
        }

        return value.S is { Length: > 0 } text ? text : throw new FormatException();
    }

    private static long? Number(Dictionary<string, AttributeValue> item, string name)
    {
        if (!item.TryGetValue(name, out var value))
        {
            return null;
        }

        return long.TryParse(value.N, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : throw new FormatException();
    }

    private static Guid RequiredGuid(Dictionary<string, AttributeValue> item, string name)
    {
        return OptionalGuid(item, name) ?? throw new FormatException();
    }

    private static Guid? OptionalGuid(Dictionary<string, AttributeValue> item, string name)
    {
        return Text(item, name) is { } text
            ? Guid.TryParseExact(text, "D", out var guid) ? guid : throw new FormatException()
            : null;
    }

    private static DateTimeOffset? OptionalTime(Dictionary<string, AttributeValue> item, string name)
    {
        return Text(item, name) is { } text
            ? AuthorityTimestamp.TryParse(text, out var time) ? time : throw new FormatException()
            : null;
    }

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return value is not null && Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal) ? Enum.Parse<TEnum>(value) : null;
    }
}

public static class DynamoDbAuthorityErrors
{
    // Mã lý do an toàn, không chứa message/ARN/request id của AWS.
    public static string Classify(Exception exception)
    {
        return exception switch
        {
            AmazonServiceException { ErrorCode: "AccessDeniedException" or "UnrecognizedClientException" or "ExpiredTokenException" }
                or AmazonServiceException { StatusCode: System.Net.HttpStatusCode.Forbidden } => "authority_access_denied",
            ResourceNotFoundException => "authority_table_not_found",
            ProvisionedThroughputExceededException or RequestLimitExceededException => "authority_throttled",
            AmazonServiceException => "authority_service_error",
            AmazonClientException => "authority_unreachable",
            _ => "authority_read_failed",
        };
    }
}

public static class DynamoDbAuthorityClientFactory
{
    // Runtime dùng default credential chain của host (IAM role chỉ có quyền GetItem trên item của deployment).
    public static IAmazonDynamoDB CreateRuntimeClient(ProvisioningAuthoritySettings settings)
    {
        return new AmazonDynamoDBClient(CreateConfig(settings));
    }

    public static AmazonDynamoDBConfig CreateConfig(ProvisioningAuthoritySettings settings)
    {
        return new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(settings.DynamoDbRegion),
            Timeout = settings.DynamoDbTimeout,
            MaxErrorRetry = 1,
        };
    }
}

// Verifier runtime chỉ gọi GetItem với ConsistentRead=true trên base table (không dùng index/scan/cache).
public sealed class DynamoDbProvisioningAuthorityReader(IAmazonDynamoDB client, ProvisioningAuthoritySettings settings)
    : IProvisioningAuthorityReader, IDisposable
{
    public async Task<ProvisioningAuthorityReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.DynamoDbTimeout!.Value);
        try
        {
            var response = await client.GetItemAsync(
                new GetItemRequest
                {
                    TableName = settings.DynamoDbTableName,
                    Key = DynamoDbAuthorityItem.Key(settings.Identity.DeploymentId),
                    ConsistentRead = true,
                },
                timeout.Token);
            return DynamoDbAuthorityItem.Parse(response.Item, settings.Identity.DeploymentId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProvisioningAuthorityReadResult.Unavailable("authority_timeout");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ProvisioningAuthorityReadResult.Unavailable(DynamoDbAuthorityErrors.Classify(ex));
        }
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
