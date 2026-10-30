using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.AuthorityService;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// R3: kiểm thử ÂM các template IAM/resource policy bằng bộ mô phỏng policy CÔ LẬP, rút gọn (Allow/Deny, wildcard, LeadingKeys,
// ArnLike/StringEquals/Bool). Đây KHÔNG phải bằng chứng AWS IAM thật thực thi đúng như vậy (C-EX2): không có SCP, permission
// boundary, session policy, ngữ cảnh điều kiện thật hay đánh giá cross-account.
public sealed class S01CIamPolicyTests
{
    private const string AccountId = AuthorityServiceTestHost.AccountId;
    private const string Region = AuthorityServiceTestHost.Region;
    private const string Table = "ikc-platform-provisioning";
    private const string ApiId = AuthorityServiceTestHost.ApiId;
    private const string Stage = AuthorityServiceTestHost.Stage;
    private static readonly Guid Deployment = Guid.Parse("0f6d6a8e-3f0e-4c1e-9d6a-2b2f0f6a7c11");
    private static readonly string TableArn = $"arn:aws:dynamodb:{Region}:{AccountId}:table/{Table}";
    private static readonly string FunctionArn = $"arn:aws:lambda:{Region}:{AccountId}:function:ikc-authority-service";

    public static TheoryData<string> OperatorWriteActions => new()
    {
        "dynamodb:UpdateItem", "dynamodb:PutItem", "dynamodb:DeleteItem", "dynamodb:BatchWriteItem", "dynamodb:ConditionCheckItem", "dynamodb:PartiQLUpdate",
    };

    [Theory]
    [MemberData(nameof(OperatorWriteActions))]
    public void DeploymentOperator_DirectAuthorityWrite_IsDenied(string action)
    {
        var policy = Load("deployment-operator.json");

        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate(action, TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Deployment))));
    }

    [Fact]
    public void DeploymentOperator_MayInvokeOperationRoutesOnly()
    {
        var policy = Load("deployment-operator.json");

        foreach (var route in AuthorityServiceRoutes.OperatorRoutes)
        {
            Assert.Equal(Decision.Allow, policy.Evaluate("execute-api:Invoke", RouteArn(route)));
        }

        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("execute-api:Invoke", RouteArn(AuthorityServiceRoutes.Approvals)));
        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("lambda:InvokeFunction", FunctionArn));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("execute-api:Invoke", $"arn:aws:execute-api:{Region}:{AccountId}:{ApiId}/{Stage}/POST/authority/operations/unknown"));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(AuthorityStoreKeys.PrincipalRegistry)));
    }

    [Fact]
    public void SecurityApprover_MayInvokeApprovalRouteOnly()
    {
        var policy = Load("security-approver.json");

        Assert.Equal(Decision.Allow, policy.Evaluate("execute-api:Invoke", RouteArn(AuthorityServiceRoutes.Approvals)));
        foreach (var route in AuthorityServiceRoutes.OperatorRoutes)
        {
            Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("execute-api:Invoke", RouteArn(route)));
        }

        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("dynamodb:PutItem", TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Deployment))));
        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("lambda:InvokeFunction", FunctionArn));
    }

    [Fact]
    public void ApplicationRuntime_ReadsOnlyItsOwnDeploymentRecord()
    {
        var policy = Load("runtime-authority-read.json");

        Assert.Equal(Decision.Allow, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Deployment))));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Guid.NewGuid()))));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(AuthorityStoreKeys.Approval(Guid.NewGuid()))));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(AuthorityStoreKeys.PrincipalRegistry)));
        foreach (var action in new[] { "dynamodb:PutItem", "dynamodb:UpdateItem", "dynamodb:Scan", "execute-api:Invoke", "lambda:InvokeFunction" })
        {
            Assert.Equal(Decision.ExplicitDeny, policy.Evaluate(action, TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Deployment))));
        }
    }

    [Fact]
    public void AuthorityService_HasScopedConditionalWritesButCannotDeleteOrRewriteRegistry()
    {
        var policy = Load("authority-service-execution-role.json");

        foreach (var key in new[] { DynamoDbAuthorityItem.PartitionKeyValue(Deployment), AuthorityStoreKeys.Approval(Guid.NewGuid()), AuthorityStoreKeys.Ledger("cli-reserve-1"), AuthorityStoreKeys.Issuance(Deployment) })
        {
            Assert.Equal(Decision.Allow, policy.Evaluate("dynamodb:PutItem", TableArn, LeadingKeys(key)));
            Assert.Equal(Decision.Allow, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(key)));
        }

        Assert.Equal(Decision.Allow, policy.Evaluate("dynamodb:GetItem", TableArn, LeadingKeys(AuthorityStoreKeys.PrincipalRegistry)));
        Assert.Equal(Decision.ExplicitDeny, policy.Evaluate("dynamodb:PutItem", TableArn, LeadingKeys(AuthorityStoreKeys.PrincipalRegistry)));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("dynamodb:PutItem", TableArn, LeadingKeys("anything#else")));
        foreach (var action in new[] { "dynamodb:DeleteItem", "dynamodb:UpdateItem", "dynamodb:Scan", "dynamodb:DeleteTable", "dynamodb:RestoreTableToPointInTime" })
        {
            Assert.Equal(Decision.ExplicitDeny, policy.Evaluate(action, TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(Deployment))));
        }
    }

    [Fact]
    public void AuthorityLambda_IsInvocableOnlyThroughTheIamAuthenticatedApi()
    {
        var policy = Load("authority-lambda-resource-policy.json");
        var viaApi = new Dictionary<string, string[]>
        {
            ["aws:PrincipalService"] = ["apigateway.amazonaws.com"],
            ["AWS:SourceArn"] = [RouteArn(AuthorityServiceRoutes.Reserve)],
            ["AWS:SourceAccount"] = [AccountId],
        };

        Assert.Equal(Decision.Allow, policy.Evaluate("lambda:InvokeFunction", FunctionArn, viaApi));

        // Gọi Lambda trực tiếp bằng credential của operator: không có principal dịch vụ API Gateway ⇒ không được phép.
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("lambda:InvokeFunction", FunctionArn, new Dictionary<string, string[]>
        {
            ["aws:PrincipalArn"] = [$"arn:aws:iam::{AccountId}:role/ikc/ikc-operator-alice"],
        }));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("lambda:InvokeFunction", FunctionArn, new Dictionary<string, string[]>(viaApi)
        {
            ["AWS:SourceArn"] = [$"arn:aws:execute-api:{Region}:{AccountId}:otherapi/{Stage}/POST/authority/operations/reserve"],
        }));
    }

    [Fact]
    public void PerPersonRoleTrust_RequiresRecentMfa()
    {
        var policy = Load("per-person-role-trust-policy.json");
        var person = $"arn:aws:iam::{AccountId}:user/alice";

        Assert.Equal(Decision.Allow, policy.Evaluate("sts:AssumeRole", "*", new Dictionary<string, string[]>
        {
            ["aws:PrincipalArn"] = [person],
            ["aws:MultiFactorAuthPresent"] = ["true"],
            ["aws:MultiFactorAuthAge"] = ["120"],
        }));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("sts:AssumeRole", "*", new Dictionary<string, string[]>
        {
            ["aws:PrincipalArn"] = [person],
            ["aws:MultiFactorAuthPresent"] = ["false"],
        }));
        Assert.Equal(Decision.ImplicitDeny, policy.Evaluate("sts:AssumeRole", "*", new Dictionary<string, string[]>
        {
            ["aws:PrincipalArn"] = [$"arn:aws:iam::{AccountId}:user/mallory"],
            ["aws:MultiFactorAuthPresent"] = ["true"],
            ["aws:MultiFactorAuthAge"] = ["10"],
        }));
    }

    // C-IR-05: với ngữ nghĩa DynamoDB, UpdateItem KHÔNG điều kiện tạo mới (upsert) bất kỳ trạng thái nào. Vì vậy IAM không bao giờ
    // được cấp quyền ghi cho operator (template ở trên từ chối tường minh), và chỉ Authority Service ghi, luôn có điều kiện.
    [Fact]
    public async Task UnconditionedUpdateItem_UpsertsAnyState_WhichIsWhyOperatorsHoldNoWritePermission()
    {
        var table = new FakeDynamoDbTable("upsert-demo");
        var deployment = Guid.NewGuid();
        var anyWriter = table.CreateClient("hypothetical-writer", allowWrites: true);

        await anyWriter.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = table.TableName,
            Key = DynamoDbAuthorityItem.Key(deployment),
            UpdateExpression = "SET #s = :s",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#s"] = DynamoDbAuthorityItem.Status },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":s"] = new() { S = nameof(ProvisioningAuthorityStatus.RecoveryPending) } },
        });

        Assert.Equal(nameof(ProvisioningAuthorityStatus.RecoveryPending), table.RawItem(DynamoDbAuthorityItem.PartitionKeyValue(deployment))![DynamoDbAuthorityItem.Status].S);
        Assert.Equal(Decision.ExplicitDeny, Load("deployment-operator.json").Evaluate("dynamodb:UpdateItem", TableArn, LeadingKeys(DynamoDbAuthorityItem.PartitionKeyValue(deployment))));
    }

    [Fact]
    public void AuthorityStoreRequests_AreAlwaysConditionalAndVersionChecked()
    {
        var next = DynamoDbAuthorityItem.ToAttributes(new ProvisioningAuthorityRecord
        {
            DeploymentId = Deployment,
            AuthorityId = "authority-1",
            Status = ProvisioningAuthorityStatus.Authorized,
            Version = 4,
            AuthorizationId = Guid.NewGuid().ToString("D"),
            AuthorizedAt = AuthorityTimestamp.Now(),
        });
        var request = DynamoDbAuthorityStore.BuildRequest(Table, "request-0001",
            [
                new AuthorityStoreWrite(DynamoDbAuthorityItem.PartitionKeyValue(Deployment), next, 3),
                new AuthorityStoreWrite(AuthorityStoreKeys.Ledger("request-0001"), AuthorityDocuments.ToItem(new { }, AuthorityDocuments.LedgerType, 1), null),
            ],
            [new AuthorityStoreCheck(AuthorityStoreKeys.Approval(Guid.NewGuid()), 2)]);

        Assert.Equal("#version = :expected", request.TransactItems[0].Put.ConditionExpression);
        Assert.Equal("attribute_not_exists(#pk)", request.TransactItems[1].Put.ConditionExpression);
        Assert.Equal("#version = :expected", request.TransactItems[2].ConditionCheck.ConditionExpression);
        Assert.Equal(32, request.ClientRequestToken.Length);

        // Version mới phải là version cũ + 1: chặn ghi đè "nhảy cóc" ngay ở lớp lưu trữ.
        Assert.Throws<InvalidOperationException>(() => DynamoDbAuthorityStore.BuildRequest(Table, "request-0002",
            [new AuthorityStoreWrite(DynamoDbAuthorityItem.PartitionKeyValue(Deployment), next, 1)], []));
    }

    private static Dictionary<string, string[]> LeadingKeys(string key) => new() { ["dynamodb:LeadingKeys"] = [key] };

    private static string RouteArn(string route) =>
        $"arn:aws:execute-api:{Region}:{AccountId}:{ApiId}/{Stage}/POST{AuthorityServiceRoutes.PathOf(route)}";

    private static SimulatedPolicy Load(string name)
    {
        using var stream = typeof(AuthorityServiceEndpoint).Assembly.GetManifestResourceStream($"Policies.{name}")
            ?? throw new InvalidOperationException($"policy_template_missing:{name}");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd()
            .Replace("${AccountId}", AccountId, StringComparison.Ordinal)
            .Replace("${Region}", Region, StringComparison.Ordinal)
            .Replace("${TableName}", Table, StringComparison.Ordinal)
            .Replace("${DeploymentId}", Deployment.ToString("D"), StringComparison.Ordinal)
            .Replace("${ApiId}", ApiId, StringComparison.Ordinal)
            .Replace("${Stage}", Stage, StringComparison.Ordinal)
            .Replace("${AuthorityFunctionArn}", FunctionArn, StringComparison.Ordinal)
            .Replace("${PersonPrincipalArn}", $"arn:aws:iam::{AccountId}:user/alice", StringComparison.Ordinal);
        Assert.DoesNotContain("${", text);
        return new SimulatedPolicy(JsonDocument.Parse(text).RootElement.Clone());
    }

    public enum Decision
    {
        Allow,
        ImplicitDeny,
        ExplicitDeny,
    }

    // Bộ mô phỏng tối giản: Deny tường minh thắng; cần ít nhất một Allow khớp; còn lại là implicit deny.
    private sealed class SimulatedPolicy(JsonElement document)
    {
        public Decision Evaluate(string action, string resource, IReadOnlyDictionary<string, string[]>? context = null)
        {
            context ??= new Dictionary<string, string[]>();
            var allowed = false;
            foreach (var statement in document.GetProperty("Statement").EnumerateArray())
            {
                if (!Values(statement, "Action").Any(pattern => Like(action, pattern))
                    || (statement.TryGetProperty("Resource", out _) && !Values(statement, "Resource").Any(pattern => Like(resource, pattern)))
                    || !PrincipalMatches(statement, context)
                    || !ConditionsMatch(statement, context))
                {
                    continue;
                }

                if (statement.GetProperty("Effect").GetString() == "Deny")
                {
                    return Decision.ExplicitDeny;
                }

                allowed = true;
            }

            return allowed ? Decision.Allow : Decision.ImplicitDeny;
        }

        private static bool PrincipalMatches(JsonElement statement, IReadOnlyDictionary<string, string[]> context)
        {
            if (!statement.TryGetProperty("Principal", out var principal))
            {
                return true;
            }

            if (principal.TryGetProperty("Service", out var service))
            {
                return context.TryGetValue("aws:PrincipalService", out var services) && services.Contains(service.GetString());
            }

            return principal.TryGetProperty("AWS", out var aws)
                && context.TryGetValue("aws:PrincipalArn", out var arns)
                && arns.Contains(aws.GetString());
        }

        private static bool ConditionsMatch(JsonElement statement, IReadOnlyDictionary<string, string[]> context)
        {
            if (!statement.TryGetProperty("Condition", out var conditions))
            {
                return true;
            }

            foreach (var block in conditions.EnumerateObject())
            {
                foreach (var condition in block.Value.EnumerateObject())
                {
                    var expected = condition.Value.ValueKind == JsonValueKind.Array
                        ? condition.Value.EnumerateArray().Select(item => item.GetString()!).ToArray()
                        : [condition.Value.GetString()!];
                    var actual = context.TryGetValue(condition.Name, out var values) ? values : [];
                    var matches = block.Name switch
                    {
                        "ForAllValues:StringEquals" => actual.Length > 0 && actual.All(value => expected.Contains(value, StringComparer.Ordinal)),
                        "ForAllValues:StringLike" => actual.Length > 0 && actual.All(value => expected.Any(pattern => Like(value, pattern))),
                        "ForAnyValue:StringLike" => actual.Any(value => expected.Any(pattern => Like(value, pattern))),
                        "StringEquals" => actual.Any(value => expected.Contains(value, StringComparer.Ordinal)),
                        "ArnLike" => actual.Any(value => expected.Any(pattern => Like(value, pattern))),
                        "Bool" => actual.Any(value => expected.Contains(value, StringComparer.OrdinalIgnoreCase)),
                        "NumericLessThan" => actual.Any(value => double.Parse(value) < double.Parse(expected[0])),
                        _ => throw new NotSupportedException(block.Name),
                    };
                    if (!matches)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerable<string> Values(JsonElement statement, string name)
        {
            var value = statement.GetProperty(name);
            return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.GetString()!) : [value.GetString()!];
        }

        private static bool Like(string value, string pattern)
        {
            return Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.CultureInvariant);
        }
    }
}
