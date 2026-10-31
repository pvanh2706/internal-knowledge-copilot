using System.Collections.Concurrent;
using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

public sealed record FakeDynamoDbCall(string Principal, string Operation, string? TableName, bool? ConsistentRead, string? Condition, string? Key = null);

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

// Bảng DynamoDB giả trong bộ nhớ, nhiều item theo "pk". Chỉ dùng để kiểm thử logic; KHÔNG phải bằng chứng tích hợp AWS
// (C-EX2) và KHÔNG chứng minh IAM (quyền ghi chỉ là cờ allowWrites của client giả).
// C-IR-05: mô phỏng đúng các ngữ nghĩa quan trọng cho bảo mật: UpdateItem/PutItem KHÔNG điều kiện sẽ tạo mới (upsert) item;
// TransactWriteItems áp dụng nguyên tử, hủy toàn bộ khi một điều kiện sai (CancellationReasons như DynamoDB).
// Hỗ trợ ngữ pháp điều kiện: attribute_exists(#x), attribute_not_exists(#x), #x = :v nối bằng AND.
public sealed class FakeDynamoDbTable(string tableName)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Dictionary<string, AttributeValue>> items = new(StringComparer.Ordinal);

    public string TableName { get; } = tableName;

    public ConcurrentQueue<FakeDynamoDbCall> Calls { get; } = new();

    // (principal, operation) → lỗi cần giả lập cho lần gọi đó.
    public Func<string, string, FakeDynamoDbFault> Fault { get; set; } = (_, _) => FakeDynamoDbFault.None;

    public IAmazonDynamoDB CreateClient(string principal, bool allowWrites) => new FakeDynamoDbClient(this, principal, allowWrites);

    // Ghi trực tiếp ngoài mọi kiểm soát (mô phỏng giả mạo bởi ai có quyền ghi bảng, hoặc dữ liệu seed của test).
    public void PutRecord(ProvisioningAuthorityRecord record)
    {
        var attributes = DynamoDbAuthorityItem.ToAttributes(record);
        attributes[DynamoDbAuthorityItem.PartitionKey] = new AttributeValue { S = DynamoDbAuthorityItem.PartitionKeyValue(record.DeploymentId) };
        PutRaw(attributes);
    }

    public void PutRaw(Dictionary<string, AttributeValue> attributes)
    {
        lock (gate)
        {
            items[attributes[DynamoDbAuthorityItem.PartitionKey].S] = Clone(attributes);
        }
    }

    // Xóa ngoài mọi kiểm soát (mô phỏng mất/restore một phần bảng).
    public void Remove(string key)
    {
        lock (gate)
        {
            items.Remove(key);
        }
    }

    public Dictionary<string, AttributeValue>? RawItem(string key)
    {
        lock (gate)
        {
            return items.TryGetValue(key, out var item) ? Clone(item) : null;
        }
    }

    public IReadOnlyList<string> Keys()
    {
        lock (gate)
        {
            return items.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
        }
    }

    public ProvisioningAuthorityRecord? Record(Guid deploymentId) =>
        DynamoDbAuthorityItem.Parse(RawItem(DynamoDbAuthorityItem.PartitionKeyValue(deploymentId)), deploymentId).Record;

    internal async Task<GetItemResponse> GetAsync(string principal, GetItemRequest request, CancellationToken cancellationToken)
    {
        var key = request.Key[DynamoDbAuthorityItem.PartitionKey].S;
        Calls.Enqueue(new FakeDynamoDbCall(principal, "GetItem", request.TableName, request.ConsistentRead, null, key));
        await ApplyFaultAsync(principal, "GetItem", cancellationToken);
        EnsureTable(request.TableName);
        return new GetItemResponse { Item = RawItem(key) };
    }

    internal async Task<UpdateItemResponse> UpdateAsync(string principal, bool allowWrites, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        var key = request.Key[DynamoDbAuthorityItem.PartitionKey].S;
        Calls.Enqueue(new FakeDynamoDbCall(principal, "UpdateItem", request.TableName, null, request.ConditionExpression, key));
        if (!allowWrites)
        {
            throw AccessDenied();
        }

        await ApplyFaultAsync(principal, "UpdateItem", cancellationToken);
        EnsureTable(request.TableName);
        lock (gate)
        {
            items.TryGetValue(key, out var current);
            if (!Evaluate(current, request.ConditionExpression, request.ExpressionAttributeNames, request.ExpressionAttributeValues))
            {
                throw new ConditionalCheckFailedException("The conditional request failed");
            }

            // Như DynamoDB: item chưa tồn tại thì UpdateItem tạo mới với khóa đã cho (upsert).
            var updated = current is null
                ? new Dictionary<string, AttributeValue> { [DynamoDbAuthorityItem.PartitionKey] = new() { S = key } }
                : Clone(current);
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

            items[key] = updated;
        }

        return new UpdateItemResponse();
    }

    internal async Task<TransactWriteItemsResponse> TransactAsync(string principal, bool allowWrites, TransactWriteItemsRequest request, CancellationToken cancellationToken)
    {
        Calls.Enqueue(new FakeDynamoDbCall(principal, "TransactWriteItems", request.TransactItems.FirstOrDefault()?.Put?.TableName,
            null, string.Join(" | ", request.TransactItems.Select(item => item.Put?.ConditionExpression ?? item.ConditionCheck?.ConditionExpression))));
        if (!allowWrites)
        {
            throw AccessDenied();
        }

        var fault = Fault(principal, "TransactWriteItems");
        if (fault != FakeDynamoDbFault.ApplyThenTimeout)
        {
            await ApplyFaultAsync(principal, "TransactWriteItems", cancellationToken, fault);
        }

        lock (gate)
        {
            var reasons = new List<CancellationReason>();
            foreach (var item in request.TransactItems)
            {
                var tableName = item.Put?.TableName ?? item.ConditionCheck!.TableName;
                EnsureTable(tableName);
                var key = item.Put is { } put ? put.Item[DynamoDbAuthorityItem.PartitionKey].S : item.ConditionCheck!.Key[DynamoDbAuthorityItem.PartitionKey].S;
                items.TryGetValue(key, out var current);
                var passed = item.Put is { } write
                    ? Evaluate(current, write.ConditionExpression, write.ExpressionAttributeNames, write.ExpressionAttributeValues)
                    : Evaluate(current, item.ConditionCheck!.ConditionExpression, item.ConditionCheck.ExpressionAttributeNames, item.ConditionCheck.ExpressionAttributeValues);
                reasons.Add(new CancellationReason { Code = passed ? "None" : "ConditionalCheckFailed" });
            }

            if (reasons.Any(reason => reason.Code != "None"))
            {
                throw new TransactionCanceledException("Transaction cancelled") { CancellationReasons = reasons };
            }

            foreach (var item in request.TransactItems.Where(item => item.Put is not null))
            {
                items[item.Put.Item[DynamoDbAuthorityItem.PartitionKey].S] = Clone(item.Put.Item);
            }
        }

        if (fault == FakeDynamoDbFault.ApplyThenTimeout)
        {
            throw new TaskCanceledException("simulated timeout after apply");
        }

        return new TransactWriteItemsResponse();
    }

    internal void RecordForbidden(string principal, string operation)
    {
        Calls.Enqueue(new FakeDynamoDbCall(principal, operation, null, null, null));
    }

    private static bool Evaluate(
        Dictionary<string, AttributeValue>? current,
        string? condition,
        Dictionary<string, string>? names,
        Dictionary<string, AttributeValue>? values)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        foreach (var clause in condition.Split(" AND "))
        {
            if (clause.StartsWith("attribute_exists(", StringComparison.Ordinal))
            {
                if (current is null || !current.ContainsKey(names![clause["attribute_exists(".Length..^1]]))
                {
                    return false;
                }

                continue;
            }

            if (clause.StartsWith("attribute_not_exists(", StringComparison.Ordinal))
            {
                if (current is not null && current.ContainsKey(names![clause["attribute_not_exists(".Length..^1]]))
                {
                    return false;
                }

                continue;
            }

            var parts = clause.Split(" = ");
            if (current is null || !current.TryGetValue(names![parts[0]], out var actual) || !Equal(actual, values![parts[1]]))
            {
                return false;
            }
        }

        return true;
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

    public override Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken cancellationToken = default)
        => table.TransactAsync(principal, allowWrites, request, cancellationToken);

    public override Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<PutItemResponse>("PutItem");

    public override Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<DeleteItemResponse>("DeleteItem");

    public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
        => Forbidden<ScanResponse>("Scan");

    public override Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        => Forbidden<QueryResponse>("Query");

    public override Task<BatchWriteItemResponse> BatchWriteItemAsync(BatchWriteItemRequest request, CancellationToken cancellationToken = default)
        => Forbidden<BatchWriteItemResponse>("BatchWriteItem");

    private Task<T> Forbidden<T>(string operation)
    {
        table.RecordForbidden(principal, operation);
        throw new NotSupportedException($"FakeDynamoDb does not support {operation}");
    }
}
