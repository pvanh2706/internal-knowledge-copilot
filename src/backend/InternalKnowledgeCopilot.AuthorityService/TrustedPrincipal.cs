using System.Text.RegularExpressions;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

// requestContext.identity do API Gateway (authorization AWS_IAM) điền sau khi xác minh chữ ký SigV4 của caller.
// Đây là nguồn danh tính DUY NHẤT; header và body do client gửi không bao giờ được dùng làm danh tính.
public sealed record GatewayCallerIdentity(string? AccountId, string? UserArn, string? Caller);

public sealed record GatewayRequestContext(string? ApiId, string? Stage, string? AuthorizationType, GatewayCallerIdentity? Identity);

// Lời gọi Authority Service. Gọi Lambda trực tiếp (không qua API Gateway) không có RequestContext nên bị từ chối.
public sealed record AuthorityInvocation(string RouteKey, string? Body, GatewayRequestContext? RequestContext);

public sealed record AuthorityServicePolicy
{
    // authority_id ghi vào mọi bản ghi deployment; runtime đối chiếu với ProvisioningAuthority:AuthorityId.
    public required string AuthorityId { get; init; }

    public required string TrustedAccountId { get; init; }

    public required string ApiId { get; init; }

    public required string Stage { get; init; }

    public TimeSpan DefaultApprovalLifetime { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan MinApprovalLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan MaxApprovalLifetime { get; init; } = TimeSpan.FromHours(4);

    // Sai lệch đồng hồ chấp nhận cho thời điểm bằng chứng do CLI gửi (bootstrap_completed_at).
    public TimeSpan EvidenceClockSkew { get; init; } = TimeSpan.FromMinutes(5);
}

// person_id đã xác minh. SessionName chỉ để truy vết (do caller tự đặt khi AssumeRole), không bao giờ dùng để định danh.
public sealed record TrustedPrincipal(
    string PersonId,
    AuthorityFunction Function,
    string RoleArn,
    string RoleUniqueId,
    string SessionName,
    IReadOnlyList<Guid> DeploymentIds)
{
    public bool MayAct(Guid deploymentId) => DeploymentIds.Contains(deploymentId);
}

public sealed record PrincipalResolution(TrustedPrincipal? Principal, string? RejectReason, string? ObservedArn)
{
    public static PrincipalResolution Reject(string reason, string? observedArn = null) => new(null, reason, observedArn);
}

public static partial class TrustedPrincipalResolver
{
    public const string IamAuthorization = "AWS_IAM";

    // Chỉ chấp nhận role riêng theo người (assumed-role). IAM user, root, federated user và anonymous đều bị từ chối.
    public static PrincipalResolution Resolve(GatewayRequestContext? context, AuthorityServicePolicy policy, PrincipalRegistryDocument? registry)
    {
        if (context?.Identity is not { } identity
            || !string.Equals(context.ApiId, policy.ApiId, StringComparison.Ordinal)
            || !string.Equals(context.Stage, policy.Stage, StringComparison.Ordinal)
            || !string.Equals(context.AuthorizationType, IamAuthorization, StringComparison.Ordinal))
        {
            return PrincipalResolution.Reject("untrusted_invocation_context");
        }

        var observedArn = identity.UserArn;
        if (!string.Equals(identity.AccountId, policy.TrustedAccountId, StringComparison.Ordinal))
        {
            return PrincipalResolution.Reject("principal_account_not_trusted", observedArn);
        }

        var arn = AssumedRoleArn().Match(identity.UserArn ?? string.Empty);
        if (!arn.Success)
        {
            return PrincipalResolution.Reject("principal_type_not_permitted", observedArn);
        }

        var caller = CallerId().Match(identity.Caller ?? string.Empty);
        if (arn.Groups["account"].Value != policy.TrustedAccountId
            || !caller.Success
            || caller.Groups["session"].Value != arn.Groups["session"].Value)
        {
            return PrincipalResolution.Reject("principal_context_inconsistent", observedArn);
        }

        if (registry is null)
        {
            return PrincipalResolution.Reject("principal_registry_unavailable", observedArn);
        }

        var roleId = caller.Groups["role_id"].Value;
        var mapping = registry.Mappings.SingleOrDefault(item => string.Equals(item.RoleUniqueId, roleId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return PrincipalResolution.Reject("principal_not_registered", observedArn);
        }

        if (!mapping.Active)
        {
            return PrincipalResolution.Reject("principal_disabled", observedArn);
        }

        // Role id (AROA...) không đổi khi role bị xóa rồi tạo lại cùng tên; đối chiếu thêm tên/tài khoản của role.
        var mappedRole = RoleArn().Match(mapping.RoleArn);
        if (!mappedRole.Success
            || mappedRole.Groups["account"].Value != policy.TrustedAccountId
            || mappedRole.Groups["name"].Value != arn.Groups["role"].Value)
        {
            return PrincipalResolution.Reject("principal_mapping_mismatch", observedArn);
        }

        return new PrincipalResolution(
            new TrustedPrincipal(mapping.PersonId, mapping.Function, mapping.RoleArn, roleId, arn.Groups["session"].Value, mapping.DeploymentIds),
            null,
            observedArn);
    }

    public static async Task<PrincipalRegistryDocument> LoadRegistryAsync(IAuthorityStore store, CancellationToken cancellationToken)
    {
        var loaded = AuthorityDocuments.FromItem<PrincipalRegistryDocument>(
            await store.GetAsync(AuthorityStoreKeys.PrincipalRegistry, cancellationToken),
            AuthorityDocuments.RegistryType) ?? throw new AuthorityDocumentInvalidException("principal_registry_missing");
        var registry = loaded.Document;
        var valid = registry.Mappings is { Count: > 0 }
            && registry.Mappings.Select(mapping => mapping.RoleUniqueId).Distinct(StringComparer.Ordinal).Count() == registry.Mappings.Count
            && registry.Mappings.All(mapping =>
                RoleIdFormat().IsMatch(mapping.RoleUniqueId ?? string.Empty)
                && ProvisioningAuthorityRecord.IsSafeIdentifier(mapping.PersonId)
                && Enum.IsDefined(mapping.Function)
                && mapping.DeploymentIds is { Count: > 0 }
                && mapping.DeploymentIds.All(id => id != Guid.Empty));
        return valid ? registry : throw new AuthorityDocumentInvalidException("principal_registry_invalid");
    }

    public static PrincipalMapping? FindOperator(PrincipalRegistryDocument registry, string personId, Guid deploymentId)
    {
        return registry.Mappings.FirstOrDefault(mapping => mapping.Active
            && mapping.Function == AuthorityFunction.Operator
            && string.Equals(mapping.PersonId, personId, StringComparison.Ordinal)
            && mapping.DeploymentIds.Contains(deploymentId));
    }

    [GeneratedRegex(@"^arn:aws:sts::(?<account>\d{12}):assumed-role/(?<role>[\w+=,.@-]{1,64})/(?<session>[\w+=,.@-]{2,64})$")]
    private static partial Regex AssumedRoleArn();

    [GeneratedRegex(@"^(?<role_id>AROA[A-Z0-9]{12,128}):(?<session>[\w+=,.@-]{2,64})$")]
    private static partial Regex CallerId();

    [GeneratedRegex(@"^AROA[A-Z0-9]{12,128}$")]
    private static partial Regex RoleIdFormat();

    [GeneratedRegex(@"^arn:aws:iam::(?<account>\d{12}):role/(?:[\w+=,.@-]+/)*(?<name>[\w+=,.@-]{1,64})$")]
    private static partial Regex RoleArn();
}
