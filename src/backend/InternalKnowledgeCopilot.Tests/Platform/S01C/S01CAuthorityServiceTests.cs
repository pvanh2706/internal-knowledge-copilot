using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.AuthorityService;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// R1 (C-IR-01): Authority Service phía server với Approval Handler / Transition Handler tách biệt. Gọi thẳng endpoint với
// ngữ cảnh API Gateway mô phỏng trên FakeDynamoDb. Bằng chứng logic, KHÔNG chứng minh API Gateway/IAM/Lambda thật (C-EX2).
public sealed class S01CAuthorityServiceTests
{
    private static readonly Guid DeploymentA = Guid.NewGuid();
    private static readonly Guid DeploymentB = Guid.NewGuid();

    private readonly AuthorityServiceTestHost host = new(new FakeDynamoDbTable("authority-service-tests"), "authority-tests", [DeploymentA, DeploymentB]);

    [Fact]
    public async Task DirectLambdaInvocation_WithoutIamAuthenticatedGatewayContext_IsRejectedBeforeAnyRead()
    {
        var body = JsonSerializer.Serialize(ApprovalBody(AuthorityApprovalAction.Bootstrap, null), AuthorityServiceJson.Options);
        var contexts = new GatewayRequestContext?[]
        {
            null,
            AuthorityServiceTestHost.ContextFor(host.Approver) with { Identity = null },
            AuthorityServiceTestHost.ContextFor(host.Approver) with { ApiId = "someotherapi" },
            AuthorityServiceTestHost.ContextFor(host.Approver) with { Stage = "dev" },
            AuthorityServiceTestHost.ContextFor(host.Approver) with { AuthorizationType = "NONE" },
        };

        foreach (var context in contexts)
        {
            var response = await host.Service.HandleAsync(new AuthorityInvocation(AuthorityServiceRoutes.Approvals, body, context));

            Assert.Equal(403, response.StatusCode);
            Assert.Equal("untrusted_invocation_context", response.Body.Reason);
        }

        Assert.Empty(host.Table.Calls);
        Assert.Null(host.Table.Record(DeploymentA));
    }

    public static TheoryData<string, string> ForgedPrincipals => new()
    {
        { "iam_user", "principal_type_not_permitted" },
        { "root", "principal_type_not_permitted" },
        { "other_account", "principal_account_not_trusted" },
        { "session_mismatch", "principal_context_inconsistent" },
        { "role_id_of_other_person", "principal_mapping_mismatch" },
        { "unregistered_role", "principal_not_registered" },
    };

    [Theory]
    [MemberData(nameof(ForgedPrincipals))]
    public async Task ForgedOrUntrustedPrincipalContext_IsRejected(string forgery, string reason)
    {
        var approver = host.Approver;
        var identity = AuthorityServiceTestHost.ContextFor(approver).Identity!;
        identity = forgery switch
        {
            "iam_user" => identity with { UserArn = $"arn:aws:iam::{AuthorityServiceTestHost.AccountId}:user/bob" },
            "root" => identity with { UserArn = $"arn:aws:iam::{AuthorityServiceTestHost.AccountId}:root" },
            "other_account" => identity with { AccountId = "999988887777" },
            "session_mismatch" => identity with { Caller = $"{approver.RoleUniqueId}:other-session" },

            // Role id của alice nhưng ARN/tên role của bob: không khớp registry.
            "role_id_of_other_person" => identity with { Caller = $"{host.OperatorAsApprover.RoleUniqueId}:cli-session" },
            _ => identity with
            {
                UserArn = $"arn:aws:sts::{AuthorityServiceTestHost.AccountId}:assumed-role/{host.Unregistered.RoleName}/cli-session",
                Caller = $"{host.Unregistered.RoleUniqueId}:cli-session",
            },
        };
        var context = AuthorityServiceTestHost.ContextFor(approver) with { Identity = identity };

        var response = await host.Service.HandleAsync(new AuthorityInvocation(
            AuthorityServiceRoutes.Approvals,
            JsonSerializer.Serialize(ApprovalBody(AuthorityApprovalAction.Bootstrap, null), AuthorityServiceJson.Options),
            context));

        Assert.Equal(reason, response.Body.Reason);
        Assert.Equal(AuthorityResponseOutcome.Rejected, response.Body.Outcome);
        Assert.Null(response.Body.CallerPersonId);
        Assert.Null(host.Table.Record(DeploymentA));
    }

    [Theory]
    [InlineData("caller_person_id")]
    [InlineData("approved_by_person_id")]
    [InlineData("role")]
    public async Task IdentityClaimsInRequestBody_AreRejected(string forgedField)
    {
        var body = JsonSerializer.SerializeToNode(ApprovalBody(AuthorityApprovalAction.Bootstrap, null), AuthorityServiceJson.Options)!.AsObject();
        body[forgedField] = "bob";

        var response = await host.Service.HandleAsync(new AuthorityInvocation(AuthorityServiceRoutes.Approvals, body.ToJsonString(), AuthorityServiceTestHost.ContextFor(host.Operator)));

        // Operator không thể tự nhận là approver bằng body: danh tính lấy từ principal (alice/Operator) và body lạ bị từ chối.
        Assert.Contains(response.Body.Reason, new[] { "approver_role_required", "invalid_request" });
        var asApprover = await host.Service.HandleAsync(new AuthorityInvocation(AuthorityServiceRoutes.Approvals, body.ToJsonString(), AuthorityServiceTestHost.ContextFor(host.Approver)));
        Assert.Equal("invalid_request", asApprover.Body.Reason);
        Assert.Null(host.Table.Record(DeploymentA));
    }

    [Fact]
    public async Task RoleSeparation_OperatorCannotApprove_AndApproverCannotOperate()
    {
        var byOperator = await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null, approver: host.Operator);
        Assert.Equal("approver_role_required", byOperator.Body.Reason);

        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var byApprover = await host.InvokeAsync(host.Approver, AuthorityServiceRoutes.Reserve, ReserveBody(approval.OperationId!.Value, 1));

        Assert.Equal("operator_role_required", byApprover.Body.Reason);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, host.Table.Record(DeploymentA)!.Status);
    }

    [Fact]
    public async Task SelfApproval_IsForbidden_EvenThroughADifferentRoleOrSession()
    {
        // alice có role Approver riêng: không được chỉ định chính mình làm operator.
        var self = await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null, operatorPersonId: "alice", approver: host.OperatorAsApprover, session: "another-session");
        Assert.Equal("self_approval_forbidden", self.Body.Reason);

        // alice phê duyệt cho carol; alice (role Operator, session khác) không dùng được approval đó.
        var forCarol = await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null, operatorPersonId: "carol", approver: host.OperatorAsApprover);
        Assert.Equal(AuthorityResponseOutcome.Applied, forCarol.Body.Outcome);
        var aliceTries = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(forCarol.Body.OperationId!.Value, 1), session: "fresh-session");
        Assert.Equal("self_approval_forbidden", aliceTries.Body.Reason);

        // Phòng thủ ở Transition Handler: approval bị sửa để người phê duyệt cũng là operator được chỉ định vẫn bị từ chối.
        var tampered = host.Approval(forCarol.Body.OperationId!.Value) with { AssignedOperatorPersonId = "alice" };
        var item = AuthorityDocuments.ToItem(tampered, AuthorityDocuments.ApprovalType, 1);
        item[AuthorityStoreKeys.PartitionKey] = new AttributeValue { S = AuthorityStoreKeys.Approval(tampered.OperationId) };
        host.Table.PutRaw(item);
        Assert.Equal("self_approval_forbidden", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(tampered.OperationId, 1))).Body.Reason);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, host.Table.Record(DeploymentA)!.Status);
    }

    [Fact]
    public async Task Approval_IsBoundToAssignedOperatorExpiryAndIsSingleUse()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var operationId = approval.OperationId!.Value;

        Assert.Equal("operator_not_assigned", (await host.InvokeAsync(host.SecondOperator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1))).Body.Reason);

        host.Clock.Offset = TimeSpan.FromHours(2);
        Assert.Equal("approval_expired", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1))).Body.Reason);

        // Approver thay approval hết hạn chưa dùng; approval cũ thành Superseded và không dùng lại được.
        var replacement = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.ReauthorizeBootstrap, 1);
        Assert.Equal(AuthorityApprovalStatus.Superseded, host.Approval(operationId).Status);
        Assert.Equal("authority_version_mismatch", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1))).Body.Reason);

        var reserved = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(replacement.OperationId!.Value, 2));
        Assert.Equal(AuthorityResponseOutcome.Applied, reserved.Body.Outcome);
        Assert.Equal("alice", reserved.Body.CallerPersonId);
        Assert.Equal("bob", reserved.Body.ApproverPersonId);
        Assert.Equal(AuthorityApprovalStatus.Consumed, host.Approval(replacement.OperationId.Value).Status);

        // Dùng lại (request mới) sau khi đã tiêu thụ: không còn ở trạng thái Authorized nên bị từ chối.
        var reuse = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(replacement.OperationId.Value, 3));
        Assert.Equal(AuthorityResponseOutcome.Rejected, reuse.Body.Outcome);
        Assert.Equal(ProvisioningAuthorityStatus.Reserved, host.Table.Record(DeploymentA)!.Status);
    }

    [Fact]
    public async Task IdempotentRetry_SameRequestIdReplaysWithoutReapplying_AndConflictingReuseIsRejected()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var body = ReserveBody(approval.OperationId!.Value, 1);

        var first = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, body);
        var transactionsAfterFirst = host.Table.Calls.Count(call => call.Operation == "TransactWriteItems");
        var replay = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, body);

        Assert.Equal(AuthorityResponseOutcome.Applied, first.Body.Outcome);
        Assert.Equal(AuthorityResponseOutcome.Replayed, replay.Body.Outcome);
        Assert.Equal(first.Body.AuthorityVersion, replay.Body.AuthorityVersion);
        Assert.Equal(transactionsAfterFirst, host.Table.Calls.Count(call => call.Operation == "TransactWriteItems"));

        Assert.Equal("request_id_conflict", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, body with { ReservedAdminId = Guid.NewGuid() })).Body.Reason);
        Assert.Equal("request_id_conflict", (await host.InvokeAsync(host.SecondOperator, AuthorityServiceRoutes.Reserve, body)).Body.Reason);
        Assert.Equal(2, host.Table.Record(DeploymentA)!.Version);
    }

    [Fact]
    public async Task UnknownStoreOutcome_IsReportedAsUnknown_AndRetryIsResolvedFromLedger()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var applyThenTimeout = true;
        host.Table.Fault = (_, operation) => operation == "TransactWriteItems" && applyThenTimeout ? FakeDynamoDbFault.ApplyThenTimeout : FakeDynamoDbFault.None;
        var body = ReserveBody(approval.OperationId!.Value, 1);

        var unknown = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, body);
        applyThenTimeout = false;
        var retry = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, body);

        Assert.Equal(503, unknown.StatusCode);
        Assert.Equal(AuthorityResponseOutcome.OutcomeUnknown, unknown.Body.Outcome);
        Assert.Equal(AuthorityResponseOutcome.Replayed, retry.Body.Outcome);
        Assert.Equal(2, host.Table.Record(DeploymentA)!.Version);
    }

    [Fact]
    public async Task StaleVersionAndConcurrentTransitions_AllowExactlyOneWinner()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var operationId = approval.OperationId!.Value;
        Assert.Equal("authority_version_mismatch", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1) with { ExpectedAuthorityVersion = 7 })).Body.Reason);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            Task.Run(() => host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1) with { RequestId = $"race-reserve-{index:D4}" }))));

        Assert.Single(results, result => result.Body.Outcome == AuthorityResponseOutcome.Applied);
        Assert.All(results.Where(result => result.Body.Outcome != AuthorityResponseOutcome.Applied), result => Assert.Equal(AuthorityResponseOutcome.Rejected, result.Body.Outcome));
        Assert.Equal(2, host.Table.Record(DeploymentA)!.Version);
    }

    [Fact]
    public async Task CrossDeploymentUse_IsRejected()
    {
        var approvalA = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        await host.MustApproveAsync(DeploymentB, AuthorityApprovalAction.Bootstrap, null);

        var crossed = await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(approvalA.OperationId!.Value, 1) with { DeploymentId = DeploymentB });
        Assert.Equal("approval_deployment_mismatch", crossed.Body.Reason);

        var unknownDeployment = Guid.NewGuid();
        Assert.Equal("deployment_not_permitted", (await host.ApproveAsync(unknownDeployment, AuthorityApprovalAction.Bootstrap, null)).Body.Reason);
        Assert.Equal(ProvisioningAuthorityStatus.Authorized, host.Table.Record(DeploymentB)!.Status);
    }

    [Fact]
    public async Task OperatorRoutes_CannotForgeAuthorizedRecoveryPendingActivatedOrLiftRevoked()
    {
        // Không có approval: operator không tạo được gì, kể cả với operation id tự chế.
        foreach (var route in AuthorityServiceRoutes.OperatorRoutes)
        {
            var response = await host.InvokeAsync(host.Operator, route, OperationBody(route, Guid.NewGuid(), 1));
            Assert.Equal(AuthorityResponseOutcome.Rejected, response.Body.Outcome);
        }

        Assert.Null(host.Table.Record(DeploymentA));

        // Activate trước reserve, hoặc complete-recovery trên bản ghi không RecoveryPending: bị từ chối ở server.
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        Assert.Equal("approval_not_current", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, approval.OperationId!.Value, 1))).Body.Reason);
        Assert.Equal("approval_action_mismatch", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.CompleteRecovery, OperationBody(AuthorityServiceRoutes.CompleteRecovery, approval.OperationId.Value, 1))).Body.Reason);

        // Revoked: không route operator hay approver nào gỡ được.
        await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Revoke, 1);
        var revoked = host.Table.Record(DeploymentA)!;
        Assert.Equal(ProvisioningAuthorityStatus.Revoked, revoked.Status);
        foreach (var route in AuthorityServiceRoutes.OperatorRoutes)
        {
            Assert.Equal(AuthorityResponseOutcome.Rejected, (await host.InvokeAsync(host.Operator, route, OperationBody(route, approval.OperationId.Value, revoked.Version))).Body.Outcome);
        }

        foreach (var action in new[] { AuthorityApprovalAction.Bootstrap, AuthorityApprovalAction.ReauthorizeBootstrap, AuthorityApprovalAction.AdminRecovery, AuthorityApprovalAction.BootstrapReconciliation, AuthorityApprovalAction.Revoke })
        {
            var response = await host.ApproveAsync(DeploymentA, action, action == AuthorityApprovalAction.Bootstrap ? null : revoked.Version, targetAdminId: action == AuthorityApprovalAction.AdminRecovery ? Guid.NewGuid() : null);
            Assert.Equal(AuthorityResponseOutcome.Rejected, response.Body.Outcome);
        }

        Assert.Equal(revoked, host.Table.Record(DeploymentA));
    }

    [Fact]
    public async Task ServerComputesStateVersionAndBootstrapAdmin_ClientCannotChooseThem()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        var operationId = approval.OperationId!.Value;
        var adminId = Guid.NewGuid();
        Assert.Equal(AuthorityResponseOutcome.Applied, (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(operationId, 1) with { ReservedAdminId = adminId })).Body.Outcome);

        // Activate không nhận state_version / admin id từ client.
        Assert.Equal("invalid_request", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, operationId, 2) with { StateVersion = 9 })).Body.Reason);
        Assert.Equal("invalid_request", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, operationId, 2) with { ReservedAdminId = Guid.NewGuid() })).Body.Reason);
        Assert.Equal("bootstrap_evidence_invalid", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, operationId, 2) with { BootstrapCompletedAt = DateTimeOffset.UtcNow.AddDays(-3) })).Body.Reason);

        Assert.Equal(AuthorityResponseOutcome.Applied, (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, operationId, 2))).Body.Outcome);
        var activated = host.Table.Record(DeploymentA)!;
        Assert.Equal(adminId, activated.BootstrapAdminId);
        Assert.Equal(1, activated.StateVersion);

        var recovery = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.AdminRecovery, 3, adminId);
        var recoveryId = recovery.OperationId!.Value;
        Assert.Equal("approval_not_claimed", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.CompleteRecovery, OperationBody(AuthorityServiceRoutes.CompleteRecovery, recoveryId, 4) with { StateVersion = 2 })).Body.Reason);
        Assert.Equal(AuthorityResponseOutcome.Applied, (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.ClaimRecovery, OperationBody(AuthorityServiceRoutes.ClaimRecovery, recoveryId, 4))).Body.Outcome);
        Assert.Equal("operator_not_assigned", (await host.InvokeAsync(host.SecondOperator, AuthorityServiceRoutes.CompleteRecovery, OperationBody(AuthorityServiceRoutes.CompleteRecovery, recoveryId, 4) with { StateVersion = 2 })).Body.Reason);
        Assert.Equal("state_version_mismatch", (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.CompleteRecovery, OperationBody(AuthorityServiceRoutes.CompleteRecovery, recoveryId, 4) with { StateVersion = 1 })).Body.Reason);
        Assert.Equal(AuthorityResponseOutcome.Applied, (await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.CompleteRecovery, OperationBody(AuthorityServiceRoutes.CompleteRecovery, recoveryId, 4) with { StateVersion = 2 })).Body.Outcome);

        var recovered = host.Table.Record(DeploymentA)!;
        Assert.Equal(ProvisioningAuthorityStatus.Activated, recovered.Status);
        Assert.Equal(2, recovered.StateVersion);
        Assert.Equal(recoveryId, recovered.LastRecoveryId);
        Assert.Equal(AuthorityApprovalStatus.Completed, host.Approval(recoveryId).Status);
    }

    [Fact]
    public async Task InitialBootstrap_CannotBeReissuedAfterRecordLossOrRestore_AndActivatedCannotBeReauthorized()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(approval.OperationId!.Value, 1));
        await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Activate, OperationBody(AuthorityServiceRoutes.Activate, approval.OperationId.Value, 2));
        Assert.Equal(ProvisioningAuthorityStatus.Activated, host.Table.Record(DeploymentA)!.Status);

        Assert.Equal("authority_state_not_eligible", (await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.ReauthorizeBootstrap, 3)).Body.Reason);

        // Bản ghi deployment mất (xóa/restore bảng một phần): tombstone lịch sử cấp phát chặn "Bootstrap lần đầu" mới.
        var tombstone = host.Table.RawItem(AuthorityStoreKeys.Issuance(DeploymentA));
        Assert.NotNull(tombstone);
        host.Table.Remove(DynamoDbAuthorityItem.PartitionKeyValue(DeploymentA));
        Assert.Equal("deployment_issuance_history_exists", (await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null)).Body.Reason);
        Assert.Null(host.Table.Record(DeploymentA));
    }

    [Fact]
    public async Task AuditEvents_CarryVerifiedIdentity_AndRejectionsCarryObservedPrincipal()
    {
        var approval = await host.MustApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null);
        await host.InvokeAsync(host.Operator, AuthorityServiceRoutes.Reserve, ReserveBody(approval.OperationId!.Value, 1));
        await host.InvokeAsync(host.Unregistered, AuthorityServiceRoutes.Reserve, ReserveBody(approval.OperationId.Value, 2));

        var events = host.Audit.Events.ToList();
        var issued = Assert.Single(events, item => item.EventType == AuthorityAuditEventTypes.ApprovalIssued);
        Assert.Equal("bob", issued.PersonId);
        Assert.Equal(host.Approver.RoleArn, issued.PrincipalRoleArn);
        var applied = Assert.Single(events, item => item.EventType == AuthorityAuditEventTypes.TransitionApplied);
        Assert.Equal("alice", applied.PersonId);
        Assert.Equal("bob", applied.ApproverPersonId);
        Assert.Equal(approval.OperationId, applied.OperationId);
        var rejected = Assert.Single(events, item => item.EventType == AuthorityAuditEventTypes.RequestRejected);
        Assert.Null(rejected.PersonId);
        Assert.Contains(host.Unregistered.RoleName, rejected.ObservedPrincipalArn);

        // Ledger bền vững ghi người thực hiện của mọi thay đổi đã áp dụng.
        var ledgers = host.Table.Keys().Where(key => key.StartsWith("ledger#", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, ledgers.Count);
        Assert.All(ledgers, key =>
        {
            var entry = AuthorityDocuments.FromItem<AuthorityLedgerEntry>(host.Table.RawItem(key), AuthorityDocuments.LedgerType)!.Value.Document;
            Assert.Contains(entry.PersonId, new[] { "alice", "bob" });
            Assert.StartsWith($"arn:aws:iam::{AuthorityServiceTestHost.AccountId}:role/", entry.PrincipalArn);
        });
    }

    [Fact]
    public async Task MissingOrInvalidPrincipalRegistry_FailsClosed()
    {
        host.SeedRegistry([DeploymentA], mapping => mapping with { PersonId = "not a safe id!" });
        Assert.Equal("principal_registry_unavailable", (await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null)).Body.Reason);

        host.SeedRegistry([DeploymentA], mapping => mapping.PersonId == "bob" ? mapping with { Active = false } : mapping);
        Assert.Equal("principal_disabled", (await host.ApproveAsync(DeploymentA, AuthorityApprovalAction.Bootstrap, null)).Body.Reason);
        Assert.Null(host.Table.Record(DeploymentA));
    }

    private static AuthorityApprovalRequest ApprovalBody(AuthorityApprovalAction action, long? expectedVersion) => new()
    {
        RequestId = $"approve-{Guid.NewGuid():N}",
        DeploymentId = DeploymentA,
        Action = action,
        ExpectedAuthorityVersion = expectedVersion,
        AssignedOperatorPersonId = "alice",
    };

    private static AuthorityOperationRequest ReserveBody(Guid operationId, long expectedVersion) => new()
    {
        RequestId = $"reserve-{Guid.NewGuid():N}",
        DeploymentId = DeploymentA,
        OperationId = operationId,
        ExpectedAuthorityVersion = expectedVersion,
        ReservedAdminId = Guid.NewGuid(),
    };

    private static AuthorityOperationRequest OperationBody(string route, Guid operationId, long expectedVersion) => new()
    {
        RequestId = $"op-{Guid.NewGuid():N}",
        DeploymentId = DeploymentA,
        OperationId = operationId,
        ExpectedAuthorityVersion = expectedVersion,
        ReservedAdminId = route == AuthorityServiceRoutes.Reserve ? Guid.NewGuid() : null,
        BootstrapCompletedAt = route == AuthorityServiceRoutes.Activate ? DateTimeOffset.UtcNow : null,
        StateVersion = route == AuthorityServiceRoutes.CompleteRecovery ? 2 : null,
    };
}
