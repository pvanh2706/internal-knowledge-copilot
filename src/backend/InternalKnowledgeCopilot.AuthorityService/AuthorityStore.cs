using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

// Mọi item trong bảng authority có "pk" và "version" (khóa lạc quan). Ghi luôn có điều kiện: item mới phải chưa tồn tại,
// item cũ phải còn đúng version đã đọc. Không có ghi vô điều kiện (UpdateItem không điều kiện của DynamoDB sẽ upsert).
public sealed record AuthorityStoreWrite(string Key, Dictionary<string, AttributeValue> Attributes, long? ExpectedVersion);

public sealed record AuthorityStoreCheck(string Key, long ExpectedVersion);

public sealed class AuthorityStoreConflictException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

// OutcomeUnknown=true: yêu cầu có thể đã được ghi (timeout sau khi gửi). Không được đoán; caller thử lại với cùng request_id.
public sealed class AuthorityStoreUnavailableException(string reason, bool outcomeUnknown, Exception? innerException = null)
    : Exception(reason, innerException)
{
    public string Reason { get; } = reason;

    public bool OutcomeUnknown { get; } = outcomeUnknown;
}

public interface IAuthorityStore
{
    // Đọc strongly consistent.
    Task<Dictionary<string, AttributeValue>?> GetAsync(string key, CancellationToken cancellationToken = default);

    // Nguyên tử: tất cả write/check cùng áp dụng hoặc không cái nào.
    Task CommitAsync(string requestId, IReadOnlyList<AuthorityStoreWrite> writes, IReadOnlyList<AuthorityStoreCheck> checks, CancellationToken cancellationToken = default);
}

public static class AuthorityStoreKeys
{
    public const string PartitionKey = DynamoDbAuthorityItem.PartitionKey;
    public const string Version = DynamoDbAuthorityItem.Version;
    public const string PrincipalRegistry = "registry#principals";

    public static string Deployment(Guid deploymentId) => DynamoDbAuthorityItem.PartitionKeyValue(deploymentId);

    public static string Approval(Guid operationId) => $"approval#{operationId:D}";

    public static string Issuance(Guid deploymentId) => $"issuance#{deploymentId:D}";

    public static string Ledger(string requestId) => $"ledger#{requestId}";

    public static long? VersionOf(Dictionary<string, AttributeValue>? item)
    {
        return item is not null && item.TryGetValue(Version, out var value)
            && long.TryParse(value.N, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var version)
                ? version
                : null;
    }
}

// Lưu trữ DynamoDB của Authority Service: GetItem(ConsistentRead) và TransactWriteItems với điều kiện version.
// ClientRequestToken chỉ là lớp idempotency phụ (hiệu lực ~10 phút); idempotency dài hạn dựa vào ledger bền vững.
public sealed class DynamoDbAuthorityStore(IAmazonDynamoDB client, string tableName, TimeSpan timeout) : IAuthorityStore
{
    public async Task<Dictionary<string, AttributeValue>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var response = await client.GetItemAsync(
                new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { [AuthorityStoreKeys.PartitionKey] = new() { S = key } },
                    ConsistentRead = true,
                },
                timeoutSource.Token);
            return response.Item is { Count: > 0 } item ? item : null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuthorityStoreUnavailableException("authority_store_timeout", outcomeUnknown: false, ex);
        }
        catch (AmazonServiceException ex)
        {
            throw new AuthorityStoreUnavailableException(DynamoDbAuthorityErrors.Classify(ex), outcomeUnknown: false, ex);
        }
        catch (AmazonClientException ex)
        {
            throw new AuthorityStoreUnavailableException("authority_store_unreachable", outcomeUnknown: false, ex);
        }
    }

    public async Task CommitAsync(string requestId, IReadOnlyList<AuthorityStoreWrite> writes, IReadOnlyList<AuthorityStoreCheck> checks, CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(tableName, requestId, writes, checks);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await client.TransactWriteItemsAsync(request, timeoutSource.Token);
        }
        catch (TransactionCanceledException ex)
        {
            // Bị hủy toàn bộ: không có gì được ghi.
            if (ex.CancellationReasons?.Any(reason => reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true)
            {
                throw new AuthorityStoreConflictException("authority_concurrent_modification");
            }

            throw new AuthorityStoreUnavailableException("authority_store_transaction_cancelled", outcomeUnknown: false, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuthorityStoreUnavailableException("authority_store_timeout", outcomeUnknown: true, ex);
        }
        catch (AmazonServiceException ex) when ((int)ex.StatusCode is >= 400 and < 500 && ex is not ProvisionedThroughputExceededException and not RequestLimitExceededException)
        {
            throw new AuthorityStoreUnavailableException(DynamoDbAuthorityErrors.Classify(ex), outcomeUnknown: false, ex);
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException)
        {
            throw new AuthorityStoreUnavailableException("authority_store_unavailable", outcomeUnknown: true, ex);
        }
    }

    public static TransactWriteItemsRequest BuildRequest(string tableName, string requestId, IReadOnlyList<AuthorityStoreWrite> writes, IReadOnlyList<AuthorityStoreCheck> checks)
    {
        var items = new List<TransactWriteItem>();
        foreach (var write in writes)
        {
            var attributes = new Dictionary<string, AttributeValue>(write.Attributes)
            {
                [AuthorityStoreKeys.PartitionKey] = new() { S = write.Key },
            };
            var newVersion = AuthorityStoreKeys.VersionOf(attributes)
                ?? throw new InvalidOperationException("authority_store_item_version_missing");
            if (newVersion != (write.ExpectedVersion ?? 0) + 1)
            {
                throw new InvalidOperationException("authority_store_item_version_invalid");
            }

            items.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = tableName,
                    Item = attributes,
                    ConditionExpression = write.ExpectedVersion is null ? "attribute_not_exists(#pk)" : "#version = :expected",
                    ExpressionAttributeNames = write.ExpectedVersion is null
                        ? new Dictionary<string, string> { ["#pk"] = AuthorityStoreKeys.PartitionKey }
                        : new Dictionary<string, string> { ["#version"] = AuthorityStoreKeys.Version },
                    ExpressionAttributeValues = write.ExpectedVersion is { } expected
                        ? new Dictionary<string, AttributeValue> { [":expected"] = new() { N = expected.ToString(CultureInfo.InvariantCulture) } }
                        : null,
                },
            });
        }

        foreach (var check in checks)
        {
            items.Add(new TransactWriteItem
            {
                ConditionCheck = new ConditionCheck
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { [AuthorityStoreKeys.PartitionKey] = new() { S = check.Key } },
                    ConditionExpression = "#version = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = AuthorityStoreKeys.Version },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":expected"] = new() { N = check.ExpectedVersion.ToString(CultureInfo.InvariantCulture) },
                    },
                },
            });
        }

        return new TransactWriteItemsRequest
        {
            TransactItems = items,
            ClientRequestToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestId)))[..32],
        };
    }
}
