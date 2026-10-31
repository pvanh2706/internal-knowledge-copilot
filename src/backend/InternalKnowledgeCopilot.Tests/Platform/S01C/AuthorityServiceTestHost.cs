using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.AuthorityService;
using InternalKnowledgeCopilot.PlatformCli.Authority;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// Principal thử nghiệm: một role AWS riêng của một người (assumed-role). Access key chỉ có ý nghĩa với gateway giả.
public sealed record TestPrincipal(string PersonId, AuthorityFunction Function, string RoleName, string RoleUniqueId, string AccessKey, string SecretKey)
{
    public string RoleArn => $"arn:aws:iam::{AuthorityServiceTestHost.AccountId}:role/ikc/{RoleName}";

    public AWSCredentials Credentials => new SessionAWSCredentials(AccessKey, SecretKey, $"session-token-{AccessKey}");
}

public sealed class TestClock : TimeProvider
{
    public TimeSpan Offset { get; set; }

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
}

public sealed class ListAuthorityAuditSink : IAuthorityAuditSink
{
    public ConcurrentQueue<AuthorityAuditEvent> Events { get; } = new();

    public void Emit(AuthorityAuditEvent auditEvent) => Events.Enqueue(auditEvent);
}

// Authority Service chạy trong tiến trình test trên FakeDynamoDb, phía trước là API Gateway GIẢ: xác minh chữ ký SigV4 bằng
// bảng credential thử nghiệm rồi điền requestContext.identity kiểu assumed-role. Đây là bằng chứng logic, KHÔNG chứng minh
// IAM, SigV4 của API Gateway thật hay MFA của STS (C-EX2).
public sealed partial class AuthorityServiceTestHost
{
    public const string AccountId = "111122223333";
    public const string ApiId = "ikcauthapi1";
    public const string Stage = "prod";
    public const string Region = "us-east-1";
    public const string ServicePrincipal = "authority-service";

    public static readonly Uri Endpoint = new($"https://{ApiId}.execute-api.{Region}.amazonaws.com/{Stage}/");

    public AuthorityServiceTestHost(FakeDynamoDbTable table, string authorityId, IReadOnlyList<Guid> deploymentIds)
    {
        Table = table;
        Policy = new AuthorityServicePolicy { AuthorityId = authorityId, TrustedAccountId = AccountId, ApiId = ApiId, Stage = Stage };
        Store = new DynamoDbAuthorityStore(table.CreateClient(ServicePrincipal, allowWrites: true), table.TableName, TimeSpan.FromSeconds(2));
        Service = new AuthorityServiceEndpoint(Store, Policy, Audit, Clock);
        Operator = Principal("alice", AuthorityFunction.Operator, "ikc-operator-alice");
        SecondOperator = Principal("carol", AuthorityFunction.Operator, "ikc-operator-carol");
        Approver = Principal("bob", AuthorityFunction.Approver, "ikc-approver-bob");

        // Cùng một người (alice) có thêm role Approver riêng: dùng để kiểm tra cấm tự phê duyệt qua role/session khác.
        OperatorAsApprover = Principal("alice", AuthorityFunction.Approver, "ikc-approver-alice");
        Unregistered = new TestPrincipal("mallory", AuthorityFunction.Operator, "ikc-operator-mallory", "AROAMALLORY0000000001", "AKIAMALLORY", "secret-mallory");
        SeedRegistry(deploymentIds);
    }

    public FakeDynamoDbTable Table { get; }

    public AuthorityServicePolicy Policy { get; }

    public DynamoDbAuthorityStore Store { get; }

    public AuthorityServiceEndpoint Service { get; }

    public ListAuthorityAuditSink Audit { get; } = new();

    public TestClock Clock { get; } = new();

    public TestPrincipal Operator { get; }

    public TestPrincipal SecondOperator { get; }

    public TestPrincipal Approver { get; }

    public TestPrincipal OperatorAsApprover { get; }

    public TestPrincipal Unregistered { get; }

    public List<TestPrincipal> Principals { get; } = [];

    // route → true: gateway xử lý xong rồi làm mất phản hồi (lỗi mạng sau khi áp dụng).
    public Func<string, bool> DropResponseAfterHandling { get; set; } = _ => false;

    // route → true: lỗi mạng trước khi tới service.
    public Func<string, bool> FailBeforeHandling { get; set; } = _ => false;

    public int HandledRequests;

    public void SeedRegistry(IReadOnlyList<Guid> deploymentIds, Func<PrincipalMapping, PrincipalMapping>? adjust = null)
    {
        var mappings = Principals.Select(principal => new PrincipalMapping
        {
            RoleUniqueId = principal.RoleUniqueId,
            RoleArn = principal.RoleArn,
            PersonId = principal.PersonId,
            Function = principal.Function,
            DeploymentIds = deploymentIds,
            Active = true,
        }).Select(mapping => adjust?.Invoke(mapping) ?? mapping).ToList();
        var item = AuthorityDocuments.ToItem(new PrincipalRegistryDocument { Mappings = mappings }, AuthorityDocuments.RegistryType, 1);
        item[AuthorityStoreKeys.PartitionKey] = new() { S = AuthorityStoreKeys.PrincipalRegistry };
        Table.PutRaw(item);
    }

    public static GatewayRequestContext ContextFor(TestPrincipal principal, string session = "cli-session") => new(
        ApiId,
        Stage,
        TrustedPrincipalResolver.IamAuthorization,
        new GatewayCallerIdentity(AccountId, $"arn:aws:sts::{AccountId}:assumed-role/{principal.RoleName}/{session}", $"{principal.RoleUniqueId}:{session}"));

    public Task<AuthorityEndpointResponse> InvokeAsync(TestPrincipal principal, string route, object body, string session = "cli-session")
    {
        return Service.HandleAsync(new AuthorityInvocation(route, JsonSerializer.Serialize(body, AuthorityServiceJson.Options), ContextFor(principal, session)));
    }

    public Task<AuthorityEndpointResponse> ApproveAsync(
        Guid deploymentId,
        AuthorityApprovalAction action,
        long? expectedVersion,
        string? operatorPersonId = "alice",
        Guid? targetAdminId = null,
        TestPrincipal? approver = null,
        int? expiresInMinutes = null,
        string session = "approver-session")
    {
        return InvokeAsync(approver ?? Approver, AuthorityServiceRoutes.Approvals, new AuthorityApprovalRequest
        {
            RequestId = $"approve-{Guid.NewGuid():N}",
            DeploymentId = deploymentId,
            Action = action,
            ExpectedAuthorityVersion = expectedVersion,
            AssignedOperatorPersonId = action == AuthorityApprovalAction.Revoke ? null : operatorPersonId,
            TargetAdminId = targetAdminId,
            ExpiresInMinutes = expiresInMinutes,
        }, session);
    }

    // Phê duyệt bắt buộc thành công (dùng để dựng trạng thái cho test).
    public async Task<AuthorityServiceResponse> MustApproveAsync(Guid deploymentId, AuthorityApprovalAction action, long? expectedVersion, Guid? targetAdminId = null, string operatorPersonId = "alice")
    {
        var response = await ApproveAsync(deploymentId, action, expectedVersion, operatorPersonId, targetAdminId);
        Assert.True(response.Body.Outcome == AuthorityResponseOutcome.Applied, response.Body.Reason);
        return response.Body;
    }

    public AuthorityApproval Approval(Guid operationId) =>
        AuthorityDocuments.FromItem<AuthorityApproval>(Table.RawItem(AuthorityStoreKeys.Approval(operationId)), AuthorityDocuments.ApprovalType)!.Value.Document;

    public HttpMessageHandler CreateGatewayHandler() => new FakeApiGatewayHandler(this);

    private TestPrincipal Principal(string person, AuthorityFunction function, string roleName)
    {
        var suffix = (Principals.Count + 1).ToString("D4");
        var principal = new TestPrincipal(person, function, roleName, $"AROATEST{person.ToUpperInvariant()}{suffix}0000", $"AKIATEST{person.ToUpperInvariant()}{suffix}", $"secret-{roleName}");
        Principals.Add(principal);
        return principal;
    }

    // API Gateway giả với authorization AWS_IAM: chữ ký sai/thiếu ⇒ 403 của gateway (không tới service).
    private sealed partial class FakeApiGatewayHandler(AuthorityServiceTestHost host) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var routeKey = $"{request.Method.Method} {request.RequestUri!.AbsolutePath[$"/{Stage}".Length..]}";
            if (host.FailBeforeHandling(routeKey))
            {
                throw new HttpRequestException("simulated connection failure");
            }

            if (Authenticate(request, payload) is not { } principal)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{\"message\":\"Forbidden\"}", Encoding.UTF8, "application/json") };
            }

            var result = await host.Service.HandleAsync(new AuthorityInvocation(routeKey, Encoding.UTF8.GetString(payload), ContextFor(principal)), cancellationToken);
            Interlocked.Increment(ref host.HandledRequests);
            if (host.DropResponseAfterHandling(routeKey))
            {
                throw new HttpRequestException("simulated lost response");
            }

            return new HttpResponseMessage((HttpStatusCode)result.StatusCode) { Content = new StringContent(result.ToJson(), Encoding.UTF8, "application/json") };
        }

        private TestPrincipal? Authenticate(HttpRequestMessage request, byte[] payload)
        {
            if (!request.Headers.TryGetValues("Authorization", out var authorization)
                || !request.Headers.TryGetValues("X-Amz-Date", out var dates)
                || AuthorizationHeader().Match(authorization.Single()) is not { Success: true } match)
            {
                return null;
            }

            var principal = host.Principals.Append(host.Unregistered).SingleOrDefault(item => item.AccessKey == match.Groups["key"].Value);
            if (principal is null
                || !DateTimeOffset.TryParseExact(dates.Single(), "yyyyMMdd'T'HHmmss'Z'", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var signedAt)
                || (DateTimeOffset.UtcNow - signedAt).Duration() > TimeSpan.FromMinutes(5))
            {
                return null;
            }

            // Tính lại chữ ký với secret của principal và so sánh.
            using var copy = new HttpRequestMessage(request.Method, request.RequestUri);
            var credentials = principal.Credentials.GetCredentials();
            SigV4Signer.Sign(copy, payload, credentials, Region, AuthorityServiceHttpClient.SigningService, signedAt);
            return copy.Headers.GetValues("Authorization").Single() == authorization.Single() ? principal : null;
        }

        [GeneratedRegex(@"^AWS4-HMAC-SHA256 Credential=(?<key>[A-Z0-9]+)/")]
        private static partial Regex AuthorizationHeader();
    }
}
