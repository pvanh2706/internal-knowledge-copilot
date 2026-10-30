using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.PlatformCli.Authority;

public enum AuthorityTransitionOutcome
{
    Applied,
    ConditionFailed,
}

// Quyền ghi authority của operator, chỉ tồn tại trong CLI (API không đăng ký). Mọi ghi là chuyển trạng thái có điều kiện:
// chỉ áp dụng khi bản ghi hiện tại vẫn là `expected`. Lỗi hạ tầng ném ProvisioningAuthorityUnavailableException; caller
// phải đọc lại để biết lần ghi có được áp dụng hay không.
public interface IProvisioningAuthorityMutator
{
    Task<AuthorityTransitionOutcome> TransitionAsync(
        ProvisioningAuthorityRecord expected,
        ProvisioningAuthorityRecord next,
        CancellationToken cancellationToken = default);
}

public static class ProvisioningAuthorityTransitions
{
    // CLI chỉ được: Authorized → Reserved, Reserved → Activated, RecoveryPending → Activated. Tạo Authorized, cấp
    // RecoveryPending hay Revoked thuộc provisioning authority độc lập, nên CLI không có code path nào làm việc đó.
    public static void EnsureOperatorTransition(ProvisioningAuthorityRecord expected, ProvisioningAuthorityRecord next)
    {
        var permitted = (expected.Status, next.Status) switch
        {
            (ProvisioningAuthorityStatus.Authorized, ProvisioningAuthorityStatus.Reserved) => true,
            (ProvisioningAuthorityStatus.Reserved, ProvisioningAuthorityStatus.Activated) => next.ReservationId == expected.ReservationId,
            (ProvisioningAuthorityStatus.RecoveryPending, ProvisioningAuthorityStatus.Activated) =>
                next.ReservationId == expected.ReservationId && next.LastRecoveryId == expected.RecoveryId,
            _ => false,
        };
        if (!permitted
            || expected.Validate() is not null
            || next.Validate() is not null
            || next.Version != expected.Version + 1
            || next.DeploymentId != expected.DeploymentId
            || !string.Equals(next.AuthorityId, expected.AuthorityId, StringComparison.Ordinal)
            || !string.Equals(next.AuthorizationId, expected.AuthorizationId, StringComparison.Ordinal)
            || next.AuthorizedAt != expected.AuthorizedAt)
        {
            throw new InvalidOperationException("authority_transition_not_permitted");
        }
    }
}

// Local seal: khóa file độc quyền + so sánh toàn bộ bản ghi thay cho conditional write.
public sealed class LocalSealProvisioningAuthorityMutator(ProvisioningAuthoritySettings settings) : IProvisioningAuthorityMutator
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(15);

    public async Task<AuthorityTransitionOutcome> TransitionAsync(
        ProvisioningAuthorityRecord expected,
        ProvisioningAuthorityRecord next,
        CancellationToken cancellationToken = default)
    {
        ProvisioningAuthorityTransitions.EnsureOperatorTransition(expected, next);
        using var sealLock = await LocalSealFile.AcquireLockAsync(settings.LocalSealPath!, LockTimeout, cancellationToken);
        var current = LocalSealFile.Read(settings.LocalSealPath!, settings.LocalSealIntegrityKey!);
        if (current.Outcome == ProvisioningAuthorityReadOutcome.Unavailable)
        {
            throw new ProvisioningAuthorityUnavailableException(current.Reason);
        }

        if (current.Record is null || current.Record != expected)
        {
            return AuthorityTransitionOutcome.ConditionFailed;
        }

        try
        {
            LocalSealFile.Write(settings.LocalSealPath!, settings.LocalSealIntegrityKey!, next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProvisioningAuthorityUnavailableException("local_seal_write_failed", ex);
        }

        return AuthorityTransitionOutcome.Applied;
    }
}

// DynamoDB: UpdateItem với ConditionExpression (attribute_exists + status + version + danh tính + reservation/recovery id).
// Không dùng PutItem nên role operator không cần (và không nên có) quyền tạo item mới.
public sealed class DynamoDbProvisioningAuthorityMutator(IAmazonDynamoDB client, ProvisioningAuthoritySettings settings)
    : IProvisioningAuthorityMutator, IDisposable
{
    public async Task<AuthorityTransitionOutcome> TransitionAsync(
        ProvisioningAuthorityRecord expected,
        ProvisioningAuthorityRecord next,
        CancellationToken cancellationToken = default)
    {
        ProvisioningAuthorityTransitions.EnsureOperatorTransition(expected, next);
        var request = BuildRequest(settings, expected, next);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.DynamoDbTimeout!.Value);
        try
        {
            await client.UpdateItemAsync(request, timeout.Token);
            return AuthorityTransitionOutcome.Applied;
        }
        catch (ConditionalCheckFailedException)
        {
            return AuthorityTransitionOutcome.ConditionFailed;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProvisioningAuthorityUnavailableException("authority_timeout", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ProvisioningAuthorityUnavailableException(DynamoDbAuthorityErrors.Classify(ex), ex);
        }
    }

    public static UpdateItemRequest BuildRequest(ProvisioningAuthoritySettings settings, ProvisioningAuthorityRecord expected, ProvisioningAuthorityRecord next)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        string Name(string attribute)
        {
            var placeholder = names.FirstOrDefault(pair => pair.Value == attribute).Key;
            if (placeholder is null)
            {
                placeholder = $"#a{names.Count}";
                names[placeholder] = attribute;
            }

            return placeholder;
        }

        string Value(AttributeValue value)
        {
            var placeholder = $":v{values.Count}";
            values[placeholder] = value;
            return placeholder;
        }

        var conditions = new List<string>
        {
            $"attribute_exists({Name(DynamoDbAuthorityItem.PartitionKey)})",
            $"{Name(DynamoDbAuthorityItem.Status)} = {Value(new AttributeValue { S = expected.Status.ToString() })}",
            $"{Name(DynamoDbAuthorityItem.Version)} = {Value(new AttributeValue { N = expected.Version.ToString(CultureInfo.InvariantCulture) })}",
            $"{Name(DynamoDbAuthorityItem.DeploymentId)} = {Value(new AttributeValue { S = expected.DeploymentId.ToString("D") })}",
            $"{Name(DynamoDbAuthorityItem.AuthorityId)} = {Value(new AttributeValue { S = expected.AuthorityId })}",
        };
        if (expected.ReservationId is { } reservationId)
        {
            conditions.Add($"{Name(DynamoDbAuthorityItem.ReservationId)} = {Value(new AttributeValue { S = reservationId.ToString("D") })}");
        }

        if (expected.RecoveryId is { } recoveryId)
        {
            conditions.Add($"{Name(DynamoDbAuthorityItem.RecoveryId)} = {Value(new AttributeValue { S = recoveryId.ToString("D") })}");
        }

        var attributes = DynamoDbAuthorityItem.ToAttributes(next);
        var assignments = attributes.Select(pair => $"{Name(pair.Key)} = {Value(pair.Value)}").ToList();
        var removals = DynamoDbAuthorityItem.OptionalAttributes.Where(attribute => !attributes.ContainsKey(attribute)).Select(Name).ToList();
        var update = "SET " + string.Join(", ", assignments) + (removals.Count > 0 ? " REMOVE " + string.Join(", ", removals) : string.Empty);

        return new UpdateItemRequest
        {
            TableName = settings.DynamoDbTableName,
            Key = DynamoDbAuthorityItem.Key(expected.DeploymentId),
            ConditionExpression = string.Join(" AND ", conditions),
            UpdateExpression = update,
            ExpressionAttributeNames = names,
            ExpressionAttributeValues = values,
        };
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
