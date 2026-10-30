using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// C3/C-G02/C-G05 với DynamoDB giả (test double). Đây là bằng chứng logic, KHÔNG phải bằng chứng tích hợp AWS production.
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
    public async Task OperatorWrites_UseConditionalUpdateItemOnly()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();

        Assert.Equal(CliExitCodes.Success, (await env.BootstrapAsync()).ExitCode);

        var writes = env.Dynamo.Calls.Where(call => call.Operation != "GetItem").ToList();
        Assert.Equal(2, writes.Count);
        Assert.All(writes, write =>
        {
            Assert.Equal("UpdateItem", write.Operation);
            Assert.Equal("operator", write.Principal);
            Assert.StartsWith("attribute_exists(", write.Condition);
            Assert.Contains(" AND ", write.Condition);
        });
        Assert.All(env.Dynamo.Calls.Where(call => call.Operation == "GetItem"), read => Assert.True(read.ConsistentRead));
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

    [Fact]
    public async Task Bootstrap_OperatorAccessDenied_IsUnavailableAndChangesNothing()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        var authorized = env.AuthorizeBootstrap();
        env.Dynamo.Fault = (principal, operation) => principal == "operator" && operation == "UpdateItem" ? FakeDynamoDbFault.AccessDenied : FakeDynamoDbFault.None;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.AuthorityUnavailable, run.ExitCode);
        Assert.Contains("authority_access_denied", run.Output);
        Assert.DoesNotContain("arn:aws", run.Output);
        Assert.DoesNotContain("secret-role-name", run.Output);
        Assert.Equal(authorized, env.CurrentAuthority());
        Assert.Equal(0, await env.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
    }

    [Fact]
    public async Task Bootstrap_ActivationTimeoutAfterSqliteCommit_ReportsReconciliationAndRuntimeDenies()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var updates = 0;
        env.Dynamo.Fault = (principal, operation) =>
            operation == "UpdateItem" && Interlocked.Increment(ref updates) == 2 ? FakeDynamoDbFault.Timeout : FakeDynamoDbFault.None;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.ReconciliationRequired, run.ExitCode);
        Assert.Contains("authority_timeout", run.Output);
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, env.CurrentAuthority()!.Status);
        var state = await env.WithDbAsync(db => db.PlatformSecurityState.AsNoTracking().SingleAsync());
        Assert.False(ProvisioningEvidence.EvaluateRuntime(state, await env.CreateReader().ReadAsync(), env.Settings.Identity).Verified);
    }

    [Fact]
    public async Task Bootstrap_ActivationAppliedThenTimeout_IsConfirmedByConsistentReread()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var updates = 0;
        env.Dynamo.Fault = (_, operation) =>
            operation == "UpdateItem" && Interlocked.Increment(ref updates) == 2 ? FakeDynamoDbFault.ApplyThenTimeout : FakeDynamoDbFault.None;

        var run = await env.BootstrapAsync();

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        Assert.Equal(ProvisioningAuthorityStatus.Activated, env.CurrentAuthority()!.Status);
    }

    [Fact]
    public async Task Mutator_StaleExpectedRecord_ConditionFails()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        var authorized = env.AuthorizeBootstrap();
        var mutator = new DynamoDbProvisioningAuthorityMutator(env.Dynamo.CreateClient("operator", allowWrites: true), env.DynamoSettings);
        var reserved = Reserve(authorized);
        Assert.Equal(AuthorityTransitionOutcome.Applied, await mutator.TransitionAsync(authorized, reserved));

        // Process khác dùng bản ghi Authorized cũ: điều kiện version/status không còn đúng.
        var competing = Reserve(authorized);
        Assert.Equal(AuthorityTransitionOutcome.ConditionFailed, await mutator.TransitionAsync(authorized, competing));
        Assert.Equal(reserved, env.CurrentAuthority());
    }

    [Fact]
    public async Task Mutator_MissingItem_IsNeverCreated()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        var authorized = AuthorizedRecord(env);
        var mutator = new DynamoDbProvisioningAuthorityMutator(env.Dynamo.CreateClient("operator", allowWrites: true), env.DynamoSettings);

        Assert.Equal(AuthorityTransitionOutcome.ConditionFailed, await mutator.TransitionAsync(authorized, Reserve(authorized)));
        Assert.Null(env.Dynamo.Raw());
    }

    public static TheoryData<string> ForbiddenTransitions => new() { "create_authorized", "grant_recovery", "revoke", "skip_version", "change_identity" };

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void OperatorTransitions_CannotCreateAuthorizationRecoveryOrRevocation(string transition)
    {
        var authorized = new ProvisioningAuthorityRecord
        {
            DeploymentId = Guid.NewGuid(),
            AuthorityId = "a",
            Status = ProvisioningAuthorityStatus.Authorized,
            Version = 1,
            AuthorizationId = "auth-1",
            AuthorizedAt = AuthorityTimestamp.Now(),
        };
        var reserved = Reserve(authorized);
        var activated = reserved with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = 3,
            BootstrapAdminId = reserved.ReservedAdminId,
            BootstrapCompletedAt = AuthorityTimestamp.Now(),
            ActivatedAt = AuthorityTimestamp.Now(),
            StateVersion = 1,
        };
        var (expected, next) = transition switch
        {
            "create_authorized" => (reserved, authorized with { Version = 3 }),
            "grant_recovery" => (activated, activated with
            {
                Status = ProvisioningAuthorityStatus.RecoveryPending,
                Version = 4,
                RecoveryId = Guid.NewGuid(),
                RecoveryPurpose = ProvisioningRecoveryPurpose.AdminRecovery,
                RecoveryTargetAdminId = Guid.NewGuid(),
                RecoveryAuthorizedAt = AuthorityTimestamp.Now(),
            }),
            "revoke" => (activated, activated with { Status = ProvisioningAuthorityStatus.Revoked, Version = 4 }),
            "skip_version" => (authorized, reserved with { Version = 5 }),
            _ => (authorized, reserved with { DeploymentId = Guid.NewGuid() }),
        };

        Assert.Throws<InvalidOperationException>(() => ProvisioningAuthorityTransitions.EnsureOperatorTransition(expected, next));
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
        var item = env.Dynamo.Raw()!;
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

    [Fact]
    public async Task Cli_DynamoDbMutationWithoutOperatorProfile_IsConfigurationErrorAndNeverFallsBackToDefaultCredentials()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        // Không có DynamoDbClientFactory: CLI phải từ chối trước khi tạo bất kỳ AWS client nào.
        var hostOptions = new PlatformCliHostOptions { EnvironmentName = "Testing", Configuration = env.Configuration };

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: hostOptions);

        Assert.Equal(CliExitCodes.ConfigurationInvalid, run.ExitCode);
        Assert.Contains("OperatorProfile", run.Output);
        Assert.Empty(env.Dynamo.Calls);
    }

    [Fact]
    public async Task Cli_ProductionMutation_RequiresPrivilegedOperatorBeforeAnyAuthorityAccess()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(useDynamoDb: true);
        env.AuthorizeBootstrap();
        var hostOptions = new PlatformCliHostOptions { EnvironmentName = "Production", Configuration = env.Configuration };

        var run = await env.RunCliAsync(["bootstrap"], hostOptions: hostOptions);

        // Không nâng quyền: bị chặn; có nâng quyền: tiếp tục bị chặn vì thiếu OperatorProfile. Không bao giờ chạm AWS.
        Assert.Equal(Environment.IsPrivilegedProcess ? CliExitCodes.ConfigurationInvalid : CliExitCodes.Denied, run.ExitCode);
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
        var properties = new[] { typeof(ProvisioningAuthorityOptions), typeof(LocalSealAuthorityOptions), typeof(DynamoDbAuthorityOptions) }
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

    private static ProvisioningAuthorityRecord AuthorizedRecord(S01CTestEnvironment env) => new()
    {
        DeploymentId = env.DeploymentId,
        AuthorityId = S01CTestEnvironment.AuthorityId,
        Status = ProvisioningAuthorityStatus.Authorized,
        Version = 1,
        AuthorizationId = "auth-1",
        AuthorizedAt = AuthorityTimestamp.Now(),
    };

    private static ProvisioningAuthorityRecord Reserve(ProvisioningAuthorityRecord authorized) => authorized with
    {
        Status = ProvisioningAuthorityStatus.Reserved,
        Version = authorized.Version + 1,
        ReservationId = Guid.NewGuid(),
        ReservedAdminId = Guid.NewGuid(),
        ReservedAt = AuthorityTimestamp.Now(),
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
