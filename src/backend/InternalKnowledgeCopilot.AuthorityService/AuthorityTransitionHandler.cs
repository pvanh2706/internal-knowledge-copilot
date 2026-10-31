using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

// Transition Handler (chỉ Deployment Operator). Sơ đồ chuyển trạng thái được cưỡng chế ở server:
//   reserve:            Authorized → Reserved              (nhận approval bootstrap, single-use)
//   activate:           Reserved → Activated               (cùng operator đã nhận approval; state_version = 1)
//   claim-recovery:     RecoveryPending (không đổi bản ghi) (nhận approval recovery, single-use)
//   complete-recovery:  RecoveryPending → Activated        (cùng operator; state_version do server tính)
// Operator không có đường nào tạo Authorized/RecoveryPending, gỡ Revoked hay tự chọn state_version/bootstrap_admin_id.
public sealed class AuthorityTransitionHandler(IAuthorityStore store, AuthorityServicePolicy policy)
{
    public async Task<AuthorityDecision> DecideAsync(
        TrustedPrincipal caller,
        string route,
        AuthorityOperationRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (caller.Function != AuthorityFunction.Operator)
        {
            return AuthorityDecision.Reject("operator_role_required", 403);
        }

        if (!caller.MayAct(request.DeploymentId))
        {
            return AuthorityDecision.Reject("deployment_not_permitted", 403);
        }

        if (InvalidShape(route, request) is { } invalid)
        {
            return AuthorityDecision.Reject(invalid, 400);
        }

        var (record, problem) = await AuthorityStateReader.ReadRecordAsync(store, policy, request.DeploymentId, cancellationToken);
        if (problem is not null)
        {
            return AuthorityDecision.Reject(problem);
        }

        if (record is null)
        {
            return AuthorityDecision.Reject("authority_record_missing");
        }

        if (await AuthorityStateReader.ReadApprovalAsync(store, request.OperationId, cancellationToken) is not { } loaded)
        {
            return AuthorityDecision.Reject("approval_not_found", 403);
        }

        var (approval, approvalVersion) = loaded;

        if (approval.DeploymentId != request.DeploymentId || approval.OperationId != request.OperationId)
        {
            return AuthorityDecision.Reject("approval_deployment_mismatch", 403);
        }

        // Phòng thủ chiều sâu: kể cả khi registry gán cả hai vai trò cho một người, người phê duyệt không thực hiện được.
        if (string.Equals(caller.PersonId, approval.ApprovedByPersonId, StringComparison.Ordinal))
        {
            return AuthorityDecision.Reject("self_approval_forbidden", 403);
        }

        if (request.ExpectedAuthorityVersion != record.Version)
        {
            return AuthorityDecision.Reject("authority_version_mismatch");
        }

        var isBootstrapApproval = approval.Action is AuthorityApprovalAction.Bootstrap or AuthorityApprovalAction.ReauthorizeBootstrap;
        var isRecoveryApproval = approval.Action is AuthorityApprovalAction.AdminRecovery or AuthorityApprovalAction.BootstrapReconciliation;
        var operationKey = request.OperationId.ToString("D");
        var decision = route switch
        {
            AuthorityServiceRoutes.Reserve when !isBootstrapApproval => AuthorityDecision.Reject("approval_action_mismatch", 403),
            AuthorityServiceRoutes.Reserve => Reserve(caller, request, record, approval, approvalVersion, operationKey, now),
            AuthorityServiceRoutes.Activate when !isBootstrapApproval => AuthorityDecision.Reject("approval_action_mismatch", 403),
            AuthorityServiceRoutes.Activate => Activate(caller, request, record, approval, approvalVersion, now),
            AuthorityServiceRoutes.ClaimRecovery when !isRecoveryApproval => AuthorityDecision.Reject("approval_action_mismatch", 403),
            AuthorityServiceRoutes.ClaimRecovery => ClaimRecovery(caller, request, record, approval, approvalVersion, now),
            AuthorityServiceRoutes.CompleteRecovery when !isRecoveryApproval => AuthorityDecision.Reject("approval_action_mismatch", 403),
            AuthorityServiceRoutes.CompleteRecovery => CompleteRecovery(caller, request, record, approval, approvalVersion, now),
            _ => AuthorityDecision.Reject("route_not_found", 404),
        };
        return decision.Accepted ? decision with { OperationId = request.OperationId, ApproverPersonId = approval.ApprovedByPersonId } : decision;
    }

    private AuthorityDecision Reserve(
        TrustedPrincipal caller,
        AuthorityOperationRequest request,
        ProvisioningAuthorityRecord record,
        AuthorityApproval approval,
        long approvalVersion,
        string operationKey,
        DateTimeOffset now)
    {
        if (record.Status != ProvisioningAuthorityStatus.Authorized
            || !string.Equals(record.AuthorizationId, operationKey, StringComparison.Ordinal)
            || record.Version != approval.AuthorityVersion)
        {
            return AuthorityDecision.Reject("approval_not_current");
        }

        if (ClaimProblem(caller, approval, now) is { } problem)
        {
            return AuthorityDecision.Reject(problem, 403);
        }

        var next = record with
        {
            Status = ProvisioningAuthorityStatus.Reserved,
            Version = record.Version + 1,
            ReservationId = request.OperationId,
            ReservedAdminId = request.ReservedAdminId,
            ReservedAt = now,
        };
        return Apply("authority_reserved", record, next, [AuthorityStateReader.ApprovalWrite(Consume(approval, caller, request, now), approvalVersion)]);
    }

    private AuthorityDecision Activate(
        TrustedPrincipal caller,
        AuthorityOperationRequest request,
        ProvisioningAuthorityRecord record,
        AuthorityApproval approval,
        long approvalVersion,
        DateTimeOffset now)
    {
        if (record.Status != ProvisioningAuthorityStatus.Reserved || record.ReservationId != request.OperationId)
        {
            return AuthorityDecision.Reject("approval_not_current");
        }

        if (HolderProblem(caller, approval) is { } problem)
        {
            return AuthorityDecision.Reject(problem, 403);
        }

        if (!PlausibleEvidenceTime(request.BootstrapCompletedAt!.Value, record.ReservedAt!.Value, now))
        {
            return AuthorityDecision.Reject("bootstrap_evidence_invalid", 400);
        }

        var next = record with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = record.Version + 1,
            BootstrapAdminId = record.ReservedAdminId,
            BootstrapCompletedAt = AuthorityTimestamp.Truncate(request.BootstrapCompletedAt.Value),
            ActivatedAt = now,
            StateVersion = 1,
        };
        return Apply("authority_activated", record, next, [AuthorityStateReader.ApprovalWrite(Complete(approval, now), approvalVersion)]);
    }

    private static AuthorityDecision ClaimRecovery(
        TrustedPrincipal caller,
        AuthorityOperationRequest request,
        ProvisioningAuthorityRecord record,
        AuthorityApproval approval,
        long approvalVersion,
        DateTimeOffset now)
    {
        if (RecoveryProblem(request, record, approval) is { } stale)
        {
            return AuthorityDecision.Reject(stale);
        }

        // Cùng operator nhận lại (ví dụ chạy lại sau lỗi SQLite): không đổi gì, không cấp quyền mới.
        if (approval.Status == AuthorityApprovalStatus.Consumed
            && string.Equals(approval.ConsumedByPersonId, caller.PersonId, StringComparison.Ordinal))
        {
            return new AuthorityDecision
            {
                Accepted = true,
                Reason = "approval_already_claimed",
                Checks = [new AuthorityStoreCheck(AuthorityStoreKeys.Deployment(record.DeploymentId), record.Version)],
                VersionBefore = record.Version,
                VersionAfter = record.Version,
            };
        }

        if (ClaimProblem(caller, approval, now) is { } problem)
        {
            return AuthorityDecision.Reject(problem, 403);
        }

        return new AuthorityDecision
        {
            Accepted = true,
            Reason = "recovery_claimed",
            Writes = [AuthorityStateReader.ApprovalWrite(Consume(approval, caller, request, now), approvalVersion)],
            Checks = [new AuthorityStoreCheck(AuthorityStoreKeys.Deployment(record.DeploymentId), record.Version)],
            VersionBefore = record.Version,
            VersionAfter = record.Version,
        };
    }

    private AuthorityDecision CompleteRecovery(
        TrustedPrincipal caller,
        AuthorityOperationRequest request,
        ProvisioningAuthorityRecord record,
        AuthorityApproval approval,
        long approvalVersion,
        DateTimeOffset now)
    {
        if (RecoveryProblem(request, record, approval) is { } stale)
        {
            return AuthorityDecision.Reject(stale);
        }

        if (HolderProblem(caller, approval) is { } problem)
        {
            return AuthorityDecision.Reject(problem, 403);
        }

        var isAdminRecovery = record.RecoveryPurpose == ProvisioningRecoveryPurpose.AdminRecovery;

        // state_version do server tính: AdminRecovery = trước + 1; BootstrapReconciliation = 2 (bootstrap 1 + reconciliation).
        var expectedStateVersion = isAdminRecovery ? record.StateVersion!.Value + 1 : 2;
        if (request.StateVersion != expectedStateVersion)
        {
            return AuthorityDecision.Reject("state_version_mismatch");
        }

        if (isAdminRecovery != request.BootstrapCompletedAt is null
            || (!isAdminRecovery && !PlausibleEvidenceTime(request.BootstrapCompletedAt!.Value, record.ReservedAt!.Value, now)))
        {
            return AuthorityDecision.Reject("bootstrap_evidence_invalid", 400);
        }

        var next = record with
        {
            Status = ProvisioningAuthorityStatus.Activated,
            Version = record.Version + 1,
            StateVersion = expectedStateVersion,
            RecoveryId = null,
            RecoveryPurpose = null,
            RecoveryTargetAdminId = null,
            RecoveryAuthorizedAt = null,
            LastRecoveryId = record.RecoveryId,
            LastRecoveredAt = now,
            RecoveryCount = record.RecoveryCount + 1,
        };
        if (!isAdminRecovery)
        {
            next = next with
            {
                BootstrapAdminId = record.ReservedAdminId,
                BootstrapCompletedAt = AuthorityTimestamp.Truncate(request.BootstrapCompletedAt!.Value),
                ActivatedAt = now,
            };
        }

        return Apply("recovery_completed", record, next, [AuthorityStateReader.ApprovalWrite(Complete(approval, now), approvalVersion)]);
    }

    private static AuthorityDecision Apply(string reason, ProvisioningAuthorityRecord record, ProvisioningAuthorityRecord next, IReadOnlyList<AuthorityStoreWrite> extraWrites)
    {
        if (next.Validate() is not null)
        {
            return AuthorityDecision.Reject("authority_transition_invalid");
        }

        return new AuthorityDecision
        {
            Accepted = true,
            Reason = reason,
            Writes = [AuthorityStateReader.RecordWrite(next, record.Version), .. extraWrites],
            VersionBefore = record.Version,
            VersionAfter = next.Version,
        };
    }

    private static string? RecoveryProblem(AuthorityOperationRequest request, ProvisioningAuthorityRecord record, AuthorityApproval approval)
    {
        var purpose = approval.Action == AuthorityApprovalAction.AdminRecovery
            ? ProvisioningRecoveryPurpose.AdminRecovery
            : ProvisioningRecoveryPurpose.BootstrapReconciliation;
        if (record.Status != ProvisioningAuthorityStatus.RecoveryPending
            || record.RecoveryId != request.OperationId
            || record.RecoveryPurpose != purpose
            || record.Version != approval.AuthorityVersion)
        {
            return "approval_not_current";
        }

        return approval.TargetAdminId == record.RecoveryTargetAdminId ? null : "approval_target_mismatch";
    }

    // Nhận approval: chỉ khi còn Issued, chưa hết hạn và caller đúng operator được chỉ định.
    private static string? ClaimProblem(TrustedPrincipal caller, AuthorityApproval approval, DateTimeOffset now)
    {
        return approval.Status switch
        {
            AuthorityApprovalStatus.Superseded => "approval_superseded",
            not AuthorityApprovalStatus.Issued => "approval_already_consumed",
            _ when now > approval.ExpiresAt => "approval_expired",
            _ when !string.Equals(approval.AssignedOperatorPersonId, caller.PersonId, StringComparison.Ordinal) => "operator_not_assigned",
            _ => null,
        };
    }

    // Hoàn tất: approval đã được chính caller nhận. Không áp dụng hạn approval (operation đang dở, SQLite có thể đã commit).
    private static string? HolderProblem(TrustedPrincipal caller, AuthorityApproval approval)
    {
        return approval.Status switch
        {
            AuthorityApprovalStatus.Issued => "approval_not_claimed",
            AuthorityApprovalStatus.Consumed when string.Equals(approval.ConsumedByPersonId, caller.PersonId, StringComparison.Ordinal) => null,
            AuthorityApprovalStatus.Consumed => "operator_not_assigned",
            _ => "approval_already_consumed",
        };
    }

    private bool PlausibleEvidenceTime(DateTimeOffset completedAt, DateTimeOffset reservedAt, DateTimeOffset now)
    {
        return completedAt >= reservedAt - policy.EvidenceClockSkew && completedAt <= now + policy.EvidenceClockSkew;
    }

    private static string? InvalidShape(string route, AuthorityOperationRequest request)
    {
        var valid = route switch
        {
            AuthorityServiceRoutes.Reserve => request.ReservedAdminId is { } admin && admin != Guid.Empty && request.BootstrapCompletedAt is null && request.StateVersion is null,
            AuthorityServiceRoutes.Activate => request.ReservedAdminId is null && request.BootstrapCompletedAt is not null && request.StateVersion is null,
            AuthorityServiceRoutes.ClaimRecovery => request.ReservedAdminId is null && request.BootstrapCompletedAt is null && request.StateVersion is null,
            AuthorityServiceRoutes.CompleteRecovery => request.ReservedAdminId is null && request.StateVersion is > 0,
            _ => false,
        };
        return valid && request.OperationId != Guid.Empty && request.DeploymentId != Guid.Empty ? null : "invalid_request";
    }

    private static AuthorityApproval Consume(AuthorityApproval approval, TrustedPrincipal caller, AuthorityOperationRequest request, DateTimeOffset now)
    {
        return approval with
        {
            Status = AuthorityApprovalStatus.Consumed,
            ConsumedByPersonId = caller.PersonId,
            ConsumedByPrincipal = caller.RoleArn,
            ConsumedAt = now,
            ConsumedRequestId = request.RequestId,
        };
    }

    private static AuthorityApproval Complete(AuthorityApproval approval, DateTimeOffset now)
    {
        return approval with { Status = AuthorityApprovalStatus.Completed, CompletedAt = now };
    }
}
