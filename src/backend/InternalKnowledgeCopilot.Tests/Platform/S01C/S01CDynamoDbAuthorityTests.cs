using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.AuthorityService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// C3/C-G02/C-G05 với DynamoDB giả (test double) và Authority Service trong tiến trình. Đây là bằng chứng logic, KHÔNG phải
// bằng chứng tích hợp AWS production và KHÔNG chứng minh IAM (C-IR-05).
public sealed class S01CDynamoDbAuthorityTests
{
    [Fact]
    public async Task RuntimeReader_OnlyIssuesStronglyConsistentGetItemOnBaseTable()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        while (env.Dynamo.Calls.TryDequeue(out _))
        {
        }

        var verifier = new ProvisioningAuthorityVerifier(env.CreateReader(), env.DynamoSettings, NullLogger<ProvisioningAuthorityVerifier>.Instance);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(await verifier.IsProvisioningVerifiedAsync(state));
        }

        var calls = env.Dynamo.Calls.ToList();
        Assert.Equal(5, calls.Count);
        Assert.All(calls, call =>
        {
            Assert.Equal("runtime", call.Principal);
            Assert.Equal("GetItem", call.Operation);
            Assert.Equal(S01CTestEnvironment.DynamoTableName, call.TableName);
            Assert.True(call.ConsistentRead);
        });
    }

    [Fact]
    public async Task AuthorityChanges_GoOnlyThroughAuthorityServiceConditionalTransactions()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();

        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);

        // approval + reserve + activate: mỗi thay đổi là một TransactWriteItems có điều kiện của Authority Service.
        var writes = env.Dynamo.Calls.Where(call => call.Operation != "GetItem").ToList();
        Assert.Equal(3, writes.Count);
        Assert.All(writes, write =>
        {
            Assert.Equal("TransactWriteItems", write.Operation);
            Assert.Equal(AuthorityServiceTestHost.ServicePrincipal, write.Principal);
            Assert.All(write.Condition!.Split(" | "), condition => Assert.False(string.IsNullOrWhiteSpace(condition)));
        });
        Assert.All(env.Dynamo.Calls.Where(call => call.Operation == "GetItem"), read => Assert.True(read.ConsistentRead));
        Assert.All(env.Dynamo.Calls.Where(call => call.Principal == "cli-read"), read =>
        {
            Assert.Equal("GetItem", read.Operation);
            Assert.Equal(DynamoDbAuthorityItem.PartitionKeyValue(env.DeploymentId), read.Key);
        });
    }

    public static TheoryData<FakeDynamoDbFault, string> ReadFaults => new()
    {
        { FakeDynamoDbFault.Timeout, "authority_timeout" },
        { FakeDynamoDbFault.Hang, "authority_timeout" },
        { FakeDynamoDbFault.AccessDenied, "authority_access_denied" },
        { FakeDynamoDbFault.Throttled, "authority_throttled" },
        { FakeDynamoDbFault.TableNotFound, "authority_table_not_found" },
    };

    [Theory]
    [MemberData(nameof(ReadFaults))]
    public async Task RuntimeReader_InfrastructureFailures_FailClosedAsUnavailable(FakeDynamoDbFault fault, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        env.Dynamo.Fault = (_, _) => fault;

        var started = DateTime.UtcNow;
        var read = await env.CreateReader().ReadAsync();
        var verified = await new ProvisioningAuthorityVerifier(env.CreateReader(), env.DynamoSettings, NullLogger<ProvisioningAuthorityVerifier>.Instance)
            .IsProvisioningVerifiedAsync(state);

        Assert.Equal(ProvisioningAuthorityReadOutcome.Unavailable, read.Outcome);
        Assert.Equal(reason, read.Reason);
        Assert.False(verified);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        var status = await env.StatusAsync();
        Assert.Equal(CliExitCodes.AuthorityUnavailable, status.ExitCode);
        Assert.DoesNotContain("arn:aws", status.Output);
    }

    [Theory]
    [InlineData("unregistered", "principal_not_registered")]
    [InlineData("other_operator", "operator_not_assigned")]
    public async Task Bootstrap_CallerNotTheAssignedRegisteredOperator_IsDeniedAndChangesNothing(string caller, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        var authorized = env.AuthorizeBootstrap();
        env.CliOperator = caller == "unregistered" ? env.AuthorityService.Unregistered : env.AuthorityService.SecondOperator;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains(reason, run.Output);
        Assert.DoesNotContain("arn:aws", run.Output);
        Assert.Equal(authorized, env.CurrentAuthority());
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    [Fact]
    public async Task Bootstrap_ActivationTimeoutAfterSqliteCommit_ReportsReconciliationAndRuntimeDenies()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var transactions = 0;

        // Giao dịch thứ nhất là reserve; mọi lần activate (kể cả retry cùng request_id) đều timeout trước khi áp dụng.
        env.Dynamo.Fault = (_, operation) =>
            operation == "TransactWriteItems" && Interlocked.Increment(ref transactions) >= 2 ? FakeDynamoDbFault.Timeout : FakeDynamoDbFault.None;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, run.ExitCode);
        Assert.Contains("authority_store_timeout", run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.False(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
    }

    // Unknown outcome: giao dịch activate đã ghi (gồm ledger) nhưng phản hồi timeout. CLI thử lại với CÙNG request_id; service
    // trả kết quả từ ledger (Replayed) thay vì áp dụng lần hai.
    [Fact]
    public async Task Bootstrap_ActivationAppliedThenTimeout_IsResolvedByLedgerReplayNotReapplied()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var transactions = 0;
        env.Dynamo.Fault = (_, operation) =>
            operation == "TransactWriteItems" && Interlocked.Increment(ref transactions) == 2 ? FakeDynamoDbFault.ApplyThenTimeout : FakeDynamoDbFault.None;

        var run = await env.BootstrapAsync();

        Assert.True(run.ExitCode == CliExitCodes.Success, run.Output);
        var authority = env.CurrentAuthority()!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, authority.Status);
        Assert.Equal(3, authority.Version);
        var events = env.AuthorityService.Audit.Events.Where(item => item.Route == AuthorityServiceRoutes.Activate).ToList();
        Assert.Contains(events, item => item.EventType == AuthorityAuditEventTypes.OutcomeUnknown);
        Assert.Contains(events, item => item.EventType == AuthorityAuditEventTypes.RequestReplayed && item.PersonId == "alice");
        Assert.DoesNotContain(events, item => item.EventType == AuthorityAuditEventTypes.TransitionApplied);
        Assert.Equal(AuthorityApprovalStatus.Completed, env.AuthorityService.Approval(Guid.Parse(authority.AuthorizationId)).Status);
    }

    public static TheoryData<string, string> MalformedItems => new()
    {
        { "status_number", "authority_record_malformed" },
        { "status_unknown", "authority_record_malformed" },
        { "record_type", "authority_record_type_invalid" },
        { "deployment_attribute", "authority_record_key_mismatch" },
        { "activated_without_evidence", "authority_record_state_invalid" },
        { "bad_timestamp", "authority_record_malformed" },
    };

    [Theory]
    [MemberData(nameof(MalformedItems))]
    public async Task Reader_MalformedOrInconsistentItems_FailClosed(string defect, string reason)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var item = env.Dynamo.RawItem(DynamoDbAuthorityItem.PartitionKeyValue(env.DeploymentId))!;
        switch (defect)
        {
            case "status_number":
                item[DynamoDbAuthorityItem.Status] = new AttributeValue { N = "2" };
                break;
            case "status_unknown":
                item[DynamoDbAuthorityItem.Status] = new AttributeValue { S = "Bypass" };
                break;
            case "record_type":
                item[DynamoDbAuthorityItem.RecordType] = new AttributeValue { S = "something_else" };
                break;
            case "deployment_attribute":
                item[DynamoDbAuthorityItem.DeploymentId] = new AttributeValue { S = Guid.NewGuid().ToString("D") };
                break;
            case "activated_without_evidence":
                item[DynamoDbAuthorityItem.Status] = new AttributeValue { S = "Activated" };
                break;
            case "bad_timestamp":
                item[DynamoDbAuthorityItem.AuthorizedAt] = new AttributeValue { S = "yesterday" };
                break;
        }

        env.Dynamo.PutRaw(item);

        var read = await env.CreateReader().ReadAsync();

        Assert.Equal(ProvisioningAuthorityReadOutcome.Malformed, read.Outcome);
        Assert.Equal(reason, read.Reason);
        Assert.False(ProvisioningEvidence.EvaluateRuntime(CompletedState(env.DeploymentId), read, env.Settings.Identity).Verified);
        Assert.Equal(CliExitCodes.Denied, (await env.BootstrapAsync()).ExitCode);
    }

    [Theory]
    [InlineData(false, "OperatorProfile")]
    [InlineData(true, "AuthorityService:Endpoint")]
    public async Task Cli_DynamoDbMutationWithoutOperatorProfileOrServiceEndpoint_IsConfigurationErrorWithoutAnyAuthorityAccess(bool removeEndpoint, string expected)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        env.Dynamo.Calls.Clear();
        var configuration = new Dictionary<string, string?>(env.Configuration);
        if (removeEndpoint)
        {
            configuration.Remove("ProvisioningAuthority:AuthorityService:Endpoint");
        }

        // Không có credential/transport của test: CLI phải từ chối trước khi tạo client hay gọi AWS; không có default chain.
        var hostOptions = new PlatformCliHostOptions { EnvironmentName = "Testing", Configuration = configuration, PrivilegeCheck = () => true };

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: hostOptions);

        Assert.Equal(CliExitCodes.ConfigurationInvalid, run.ExitCode);
        Assert.Contains(expected, run.Output);
        Assert.Empty(env.Dynamo.Calls);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Cli_Mutation_RequiresElevatedOperatorInEveryEnvironmentBeforeAnyAuthorityAccess(string environmentName)
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        env.Dynamo.Calls.Clear();

        // C-IR-03: không có PrivilegeCheck thay thế ⇒ dùng Environment.IsPrivilegedProcess thật, không phụ thuộc tên môi trường.
        var hostOptions = new PlatformCliHostOptions { EnvironmentName = environmentName, Configuration = env.Configuration };

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: hostOptions);

        if (Environment.IsPrivilegedProcess)
        {
            Assert.Equal(CliExitCodes.ConfigurationInvalid, run.ExitCode);
        }
        else
        {
            Assert.Equal(CliExitCodes.Denied, run.ExitCode);
            Assert.Contains("privileged_operator_required", run.Output);
        }

        Assert.Empty(env.Dynamo.Calls);
    }

    [Theory]
    [InlineData("DynamoDb", null, "us-east-1")]
    [InlineData("DynamoDb", "table", "not a region")]
    [InlineData("LocalSeal", null, null)]
    [InlineData("Bypass", null, null)]
    [InlineData("none ", null, null)]
    public void ConfigurationValidator_RejectsIncompleteOrUnsupportedProviders(string provider, string? table, string? region)
    {
        var options = new ProvisioningAuthorityOptions
        {
            Provider = provider,
            DeploymentId = Guid.NewGuid().ToString(),
            AuthorityId = "authority-1",
            DynamoDb = new DynamoDbAuthorityOptions { TableName = table, Region = region },
        };

        Assert.Throws<InvalidOperationException>(() => ProvisioningAuthorityConfigurationValidator.Validate(options, "Testing", Path.Combine(Path.GetTempPath(), "db", "x.db")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ConfigurationValidator_LocalSealOutsideDevelopmentOrTesting_IsRejected(string environmentName)
    {
        var options = LocalSealOptions(Path.Combine(Path.GetTempPath(), "seal", "seal.json"));

        var error = Assert.Throws<InvalidOperationException>(() => ProvisioningAuthorityConfigurationValidator.Validate(options, environmentName, Path.Combine(Path.GetTempPath(), "db", "x.db")));
        Assert.Contains("LocalSeal", error.Message);
    }

    [Fact]
    public void ConfigurationValidator_LocalSealInsideDatabaseDirectory_IsRejected()
    {
        var databaseDirectory = Path.Combine(Path.GetTempPath(), $"ikc-{Guid.NewGuid():N}");
        var options = LocalSealOptions(Path.Combine(databaseDirectory, "nested", "seal.json"));

        Assert.Throws<InvalidOperationException>(() => ProvisioningAuthorityConfigurationValidator.Validate(options, "Testing", Path.Combine(databaseDirectory, "app.db")));
    }

    [Fact]
    public void ConfigurationOptions_ExposeNoBypassOrSkipSwitch()
    {
        var properties = new[] { typeof(ProvisioningAuthorityOptions), typeof(LocalSealAuthorityOptions), typeof(DynamoDbAuthorityOptions), typeof(AuthorityServiceOptions) }
            .SelectMany(type => type.GetProperties())
            .ToList();

        Assert.DoesNotContain(properties, property => property.PropertyType == typeof(bool));
        Assert.DoesNotContain(properties, property => new[] { "bypass", "skip", "disable", "insecure", "fallback" }
            .Any(word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static ProvisioningAuthorityOptions LocalSealOptions(string path) => new()
    {
        Provider = "LocalSeal",
        DeploymentId = Guid.NewGuid().ToString(),
        AuthorityId = "authority-1",
        LocalSeal = new LocalSealAuthorityOptions { Path = path, IntegrityKey = Convert.ToBase64String(new byte[32]) },
    };

    private static PlatformSecurityStateEntity CompletedState(Guid deploymentId) => new()
    {
        DeploymentId = deploymentId,
        BootstrapStatus = Api.Common.PlatformBootstrapStatus.Completed,
        BootstrapAdminId = Guid.NewGuid(),
        BootstrapCompletedAt = AuthorityTimestamp.Now(),
        StateVersion = 1,
    };
}
