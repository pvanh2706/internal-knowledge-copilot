using System.Collections.Concurrent;
using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

public sealed record FakeDynamoDbCall(string Principal, string Operation, string? TableName, bool? ConsistentRead, string? Condition);

public enum FakeDynamoDbFault
{
    None,
    AccessDenied,
    Timeout,
    Hang,
    ApplyThenTimeout,
    Throttled,
    TableNotFound,
}

// Bảng DynamoDB giả trong bộ nhớ (một item). Chỉ dùng để kiểm thử logic; KHÔNG phải bằng chứng tích hợp AWS (C-EX2).
// Đánh giá đúng ngữ pháp ConditionExpression/UpdateExpression mà mutator sinh ra; mọi API khác của IAmazonDynamoDB bị từ chối
// và ghi nhận (để chứng minh runtime chỉ gọi GetItem).
public sealed class FakeDynamoDbTable(string tableName)
{
    private readonly object gate = new();
    private Dictionary<string, AttributeValue>? item;

    public string TableName { get; } = tableName;

    public ConcurrentQueue<FakeDynamoDbCall> Calls { get; } = new();

    // (principal, operation) → lỗi cần giả lập cho lần gọi đó.
    public Func<string, string, FakeDynamoDbFault> Fault { get; set; } = (_, _) => FakeDynamoDbFault.None;

    public IAmazonDynamoDB CreateClient(string principal, bool allowWrites) => new FakeDynamoDbClient(this, principal, allowWrites);

    // Ghi trực tiếp, mô phỏng provisioning authority độc lập (ngoài CLI).
    public void PutRecord(ProvisioningAuthorityRecord record)
    {
        var attributes = DynamoDbAuthorityItem.ToAttributes(record);
        foreach (var (key, value) in DynamoDbAuthorityItem.Key(record.DeploymentId))
        {
            attributes[key] = value;
        }

        PutRaw(attributes);
    }

    public void PutRaw(Dictionary<string, AttributeValue> attributes)
    {
        lock (gate)
        {
            item = Clone(attributes);
        }
    }

    public Dictionary<string, AttributeValue>? Raw()
    {
        lock (gate)
        {
            return item is null ? null : Clone(item);
        }
    }

    public ProvisioningAuthorityRecord? Record(Guid deploymentId) => DynamoDbAuthorityItem.Parse(Raw(), deploymentId).Record;

    internal async Task<GetItemResponse> GetAsync(string principal, GetItemRequest request, CancellationToken cancellationToken)
    {
        Calls.Enqueue(new FakeDynamoDbCall(principal, "GetItem", request.TableName, request.ConsistentRead, null));
        await ApplyFaultAsync(principal, "GetItem", cancellationToken);
        EnsureTable(request.TableName);
        lock (gate)
        {
            var matches = item is not null && Equal(item[DynamoDbAuthorityItem.PartitionKey], request.Key[DynamoDbAuthorityItem.PartitionKey]);
            return new GetItemResponse { Item = matches ? Clone(item!) : null };
        }
    }

    internal async Task<UpdateItemResponse> UpdateAsync(string principal, bool allowWrites, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        Calls.Enqueue(new FakeDynamoDbCall(principal, "UpdateItem", request.TableName, null, request.ConditionExpression));
        if (!allowWrites)
        {
            throw AccessDenied();
        }

        var fault = Fault(principal, "UpdateItem");
        if (fault != FakeDynamoDbFault.ApplyThenTimeout)
        {
            await ApplyFaultAsync(principal, "UpdateItem", cancellationToken, fault);
        }

        EnsureTable(request.TableName);
        lock (gate)
        {
            if (item is null || !Equal(item[DynamoDbAuthorityItem.PartitionKey], request.Key[DynamoDbAuthorityItem.PartitionKey]))
            {
                throw new ConditionalCheckFailedException("The conditional request failed");
            }

            foreach (var condition in request.ConditionExpression.Split(" AND "))
            {
                if (condition.StartsWith("attribute_exists(", StringComparison.Ordinal))
                {
                    var name = request.ExpressionAttributeNames[condition["attribute_exists(".Length..^1]];
                    if (!item.ContainsKey(name))
                    {
                        throw new ConditionalCheckFailedException("The conditional request failed");
                    }

                    continue;
                }

                var parts = condition.Split(" = ");
                var attribute = request.ExpressionAttributeNames[parts[0]];
                if (!item.TryGetValue(attribute, out var actual) || !Equal(actual, request.ExpressionAttributeValues[parts[1]]))
                {
                    throw new ConditionalCheckFailedException("The conditional request failed");
                }
            }

            var updated = Clone(item);
            var sections = request.UpdateExpression.Split(" REMOVE ");
            foreach (var assignment in sections[0]["SET ".Length..].Split(", "))
            {
                var parts = assignment.Split(" = ");
                var attribute = request.ExpressionAttributeNames[parts[0]];
                if (attribute == DynamoDbAuthorityItem.PartitionKey)
                {
                    throw new AmazonDynamoDBException("Cannot update key attribute");
                }

                updated[attribute] = request.ExpressionAttributeValues[parts[1]];
            }

            if (sections.Length == 2)
            {
                foreach (var placeholder in sections[1].Split(", "))
                {
                    updated.Remove(request.ExpressionAttributeNames[placeholder]);
                }
            }

            item = updated;
        }

        if (fault == FakeDynamoDbFault.ApplyThenTimeout)
        {
            throw new TaskCanceledException("simulated timeout after apply");
        }

        return new UpdateItemResponse();
    }

    internal void RecordForbidden(string principal, string operation)
    {
        Calls.Enqueue(new FakeDynamoDbCall(principal, operation, null, null, null));
    }

    private async Task ApplyFaultAsync(string principal, string operation, CancellationToken cancellationToken, FakeDynamoDbFault? known = null)
    {
        switch (known ?? Fault(principal, operation))
        {
            case FakeDynamoDbFault.AccessDenied:
                throw AccessDenied();
            case FakeDynamoDbFault.Timeout:
            case FakeDynamoDbFault.ApplyThenTimeout:
                throw new TaskCanceledException("simulated timeout");
            case FakeDynamoDbFault.Hang:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                break;
            case FakeDynamoDbFault.Throttled:
                throw new ProvisionedThroughputExceededException("throttled");
            case FakeDynamoDbFault.TableNotFound:
                throw new ResourceNotFoundException("Requested resource not found");
        }
    }

    private void EnsureTable(string requested)
    {
        if (!string.Equals(requested, TableName, StringComparison.Ordinal))
        {
            throw new ResourceNotFoundException("Requested resource not found");
        }
    }

    private static AmazonDynamoDBException AccessDenied()
    {
        return new AmazonDynamoDBException("User is not authorized (arn:aws:iam::123456789012:role/secret-role-name)", ErrorType.Sender, "AccessDeniedException", "req-1", HttpStatusCode.BadRequest);
    }

    private static bool Equal(AttributeValue left, AttributeValue right) => left.S == right.S && left.N == right.N;

    private static Dictionary<string, AttributeValue> Clone(Dictionary<string, AttributeValue> source)
    {
        return source.ToDictionary(pair => pair.Key, pair => new AttributeValue { S = pair.Value.S, N = pair.Value.N });
    }
}

// Subclass client thật của SDK, override các operation dùng tới. ServiceURL trỏ vào cổng loopback không lắng nghe nên
// bất kỳ operation nào không được override cũng thất bại cục bộ, không bao giờ gửi tới AWS.
public sealed class FakeDynamoDbClient(FakeDynamoDbTable table, string principal, bool allowWrites)
    : AmazonDynamoDBClient(new AnonymousAWSCredentials(), new AmazonDynamoDBConfig { ServiceURL = "http://127.0.0.1:9", MaxErrorRetry = 0 })
{
    public override Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
        => table.GetAsync(principal, request, cancellationToken);

    public override Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest request, CancellationToken cancellationToken = default)
        => table.UpdateAsync(principal, allowWrites, request, cancellationToken);

    public override Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<PutItemResponse>("PutItem");

    public override Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<DeleteItemResponse>("DeleteItem");

    public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
        => Forbidden<ScanResponse>("Scan");

    public override Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        => Forbidden<QueryResponse>("Query");

    public override Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken cancellationToken = default)
        => Forbidden<TransactWriteItemsResponse>("TransactWriteItems");

    public override Task<BatchWriteItemResponse> BatchWriteItemAsync(BatchWriteItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<BatchWriteItemResponse>("BatchWriteItem");

    private Task<T> Forbidden<T>(string operation)
    {
        table.RecordForbidden(principal, operation);
        throw new NotSupportedException($"FakeDynamoDb does not support {operation}");
    }
}
