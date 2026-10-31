using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

// Approval Handler (chỉ Security Approver). Tạo các trạng thái mà operator không bao giờ tạo được: Authorized,
// RecoveryPending, Revoked. Mỗi approval cho operator gắn với deployment, action, target, version authority sau phê duyệt,
// operator được chỉ định (khác người phê duyệt), thời hạn và operation id do service cấp.
public sealed class AuthorityApprovalHandler(IAuthorityStore store, AuthorityServicePolicy policy)
{
    public async Task<AuthorityDecision> DecideAsync(
        TrustedPrincipal caller,
        AuthorityApprovalRequest request,
        PrincipalRegistryDocument registry,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (caller.Function != AuthorityFunction.Approver)
        {
            return AuthorityDecision.Reject("approver_role_required", 403);
        }

        if (!caller.MayAct(request.DeploymentId))
        {
            return AuthorityDecision.Reject("deployment_not_permitted", 403);
        }

        var (record, problem) = await AuthorityStateReader.ReadRecordAsync(store, policy, request.DeploymentId, cancellationToken);
        if (problem is not null)
        {
            return AuthorityDecision.Reject(problem);
        }

        if (request.Action == AuthorityApprovalAction.Revoke)
        {
            return DecideRevoke(request, record);
        }

        // Tách nhiệm vụ: operator được chỉ định phải là một người khác, đang hoạt động, có role Operator cho deployment này.
        if (request.AssignedOperatorPersonId is not { } operatorId
            || TrustedPrincipalResolver.FindOperator(registry, operatorId, request.DeploymentId) is null)
        {
            return AuthorityDecision.Reject("assigned_operator_invalid", 400);
        }

        if (string.Equals(operatorId, caller.PersonId, StringComparison.Ordinal))
        {
            return AuthorityDecision.Reject("self_approval_forbidden", 403);
        }

        var lifetime = request.ExpiresInMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : policy.DefaultApprovalLifetime;
        if (lifetime < policy.MinApprovalLifetime || lifetime > policy.MaxApprovalLifetime)
        {
            return AuthorityDecision.Reject("approval_lifetime_invalid", 400);
        }

        var needsTarget = request.Action == AuthorityApprovalAction.AdminRecovery;
        var hasTarget = request.TargetAdminId is { } target && target != Guid.Empty;
        if (needsTarget != hasTarget)
        {
            return AuthorityDecision.Reject("approval_target_invalid", 400);
        }

        var operationId = Guid.NewGuid();
        var writes = new List<AuthorityStoreWrite>();
        ProvisioningAuthorityRecord next;
        switch (request.Action)
        {
            case AuthorityApprovalAction.Bootstrap:
            {
                // Lần đầu duy nhất: không có bản ghi và không có lịch sử cấp phát (kể cả khi bản ghi đã bị xóa/restore).
                if (record is not null)
                {
                    return AuthorityDecision.Reject("authority_record_exists");
                }

                if (await ReadIssuanceAsync(request.DeploymentId, cancellationToken) is not null)
                {
                    return AuthorityDecision.Reject("deployment_issuance_history_exists");
                }

                if (request.ExpectedAuthorityVersion is not null)
                {
                    return AuthorityDecision.Reject("authority_version_mismatch");
                }

                next = new ProvisioningAuthorityRecord
                {
                    DeploymentId = request.DeploymentId,
                    AuthorityId = policy.AuthorityId,
                    Status = ProvisioningAuthorityStatus.Authorized,
                    Version = 1,
                    AuthorizationId = operationId.ToString("D"),
                    AuthorizedAt = now,
                };
                writes.Add(IssuanceWrite(new AuthorityIssuanceHistory
                {
                    DeploymentId = request.DeploymentId,
                    BootstrapIssuanceCount = 1,
                    FirstIssuedAt = now,
                    LastIssuedAt = now,
                    LastOperationId = operationId,
                }, null));
                break;
            }

            case AuthorityApprovalAction.ReauthorizeBootstrap:
            {
                // Không tự mở lại: chỉ sau reservation bị gián đoạn (approver đã điều tra, không có bằng chứng SQLite) hoặc
                // để thay approval bootstrap hết hạn chưa dùng. Không bao giờ từ Activated, RecoveryPending hay Revoked.
                if (record is null)
                {
                    return AuthorityDecision.Reject("authority_record_missing");
                }

                if (await ReadIssuanceAsync(request.DeploymentId, cancellationToken) is not { } issuance)
                {
                    return AuthorityDecision.Reject("deployment_issuance_history_missing");
                }

                if (await CheckVersionAndSupersedeAsync(request, record, ProvisioningAuthorityStatus.Reserved, ProvisioningAuthorityStatus.Authorized, null, now, writes, cancellationToken) is { } rejected)
                {
                    return rejected;
                }

                next = new ProvisioningAuthorityRecord
                {
                    DeploymentId = record.DeploymentId,
                    AuthorityId = record.AuthorityId,
                    Status = ProvisioningAuthorityStatus.Authorized,
                    Version = record.Version + 1,
                    AuthorizationId = operationId.ToString("D"),
                    AuthorizedAt = now,
                };
                writes.Add(IssuanceWrite(issuance.Document with
                {
                    BootstrapIssuanceCount = issuance.Document.BootstrapIssuanceCount + 1,
                    LastIssuedAt = now,
                    LastOperationId = operationId,
                }, issuance.Version));
                break;
            }

            case AuthorityApprovalAction.AdminRecovery:
            case AuthorityApprovalAction.BootstrapReconciliation:
            {
                if (record is null)
                {
                    return AuthorityDecision.Reject("authority_record_missing");
                }

                var isAdminRecovery = request.Action == AuthorityApprovalAction.AdminRecovery;
                var purpose = isAdminRecovery ? ProvisioningRecoveryPurpose.AdminRecovery : ProvisioningRecoveryPurpose.BootstrapReconciliation;
                var origin = isAdminRecovery ? ProvisioningAuthorityStatus.Activated : ProvisioningAuthorityStatus.Reserved;
                if (await CheckVersionAndSupersedeAsync(request, record, origin, ProvisioningAuthorityStatus.RecoveryPending, purpose, now, writes, cancellationToken) is { } rejected)
                {
                    return rejected;
                }

                next = record with
                {
                    Status = ProvisioningAuthorityStatus.RecoveryPending,
                    Version = record.Version + 1,
                    RecoveryId = operationId,
                    RecoveryPurpose = purpose,
                    RecoveryTargetAdminId = isAdminRecovery ? request.TargetAdminId : null,
                    RecoveryAuthorizedAt = now,
                };
                break;
            }

            default:
                return AuthorityDecision.Reject("approval_action_invalid", 400);
        }

        if (next.Validate() is not null)
        {
            return AuthorityDecision.Reject("authority_transition_invalid");
        }

        writes.Add(AuthorityStateReader.RecordWrite(next, record?.Version));
        writes.Add(AuthorityStateReader.ApprovalWrite(new AuthorityApproval
        {
            OperationId = operationId,
            DeploymentId = request.DeploymentId,
            Action = request.Action,
            TargetAdminId = request.TargetAdminId,
            AuthorityVersion = next.Version,
            AssignedOperatorPersonId = operatorId,
            ApprovedByPersonId = caller.PersonId,
            ApprovedByPrincipal = caller.RoleArn,
            ApprovedAt = now,
            ExpiresAt = now + lifetime,
            Status = AuthorityApprovalStatus.Issued,
            IssuingRequestId = request.RequestId,
        }, null));
        return new AuthorityDecision
        {
            Accepted = true,
            Reason = "approval_issued",
            Writes = writes,
            OperationId = operationId,
            ApproverPersonId = caller.PersonId,
            VersionBefore = record?.Version,
            VersionAfter = next.Version,
        };
    }

    private static AuthorityDecision DecideRevoke(AuthorityApprovalRequest request, ProvisioningAuthorityRecord? record)
    {
        if (request.AssignedOperatorPersonId is not null || request.TargetAdminId is not null || request.ExpiresInMinutes is not null)
        {
            return AuthorityDecision.Reject("invalid_request", 400);
        }

        if (record is null)
        {
            return AuthorityDecision.Reject("authority_record_missing");
        }

        if (record.Status == ProvisioningAuthorityStatus.Revoked)
        {
            return AuthorityDecision.Reject("authority_already_revoked");
        }

        if (request.ExpectedAuthorityVersion != record.Version)
        {
            return AuthorityDecision.Reject("authority_version_mismatch");
        }

        var revoked = record with { Status = ProvisioningAuthorityStatus.Revoked, Version = record.Version + 1 };
        return new AuthorityDecision
        {
            Accepted = true,
            Reason = "authority_revoked",
            Writes = [AuthorityStateReader.RecordWrite(revoked, record.Version)],
            VersionBefore = record.Version,
            VersionAfter = revoked.Version,
        };
    }

    // Trạng thái nguồn hợp lệ là `origin`; hoặc `pendingStatus` (cùng purpose) khi approval đang chờ đã hết hạn mà chưa ai nhận,
    // lúc đó approval cũ được đánh dấu Superseded trong cùng transaction.
    private async Task<AuthorityDecision?> CheckVersionAndSupersedeAsync(
        AuthorityApprovalRequest request,
        ProvisioningAuthorityRecord record,
        ProvisioningAuthorityStatus origin,
        ProvisioningAuthorityStatus pendingStatus,
        ProvisioningRecoveryPurpose? pendingPurpose,
        DateTimeOffset now,
        List<AuthorityStoreWrite> writes,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedAuthorityVersion != record.Version)
        {
            return AuthorityDecision.Reject("authority_version_mismatch");
        }

        if (record.Status == origin)
        {
            return null;
        }

        if (record.Status != pendingStatus || record.RecoveryPurpose != pendingPurpose)
        {
            return AuthorityDecision.Reject("authority_state_not_eligible");
        }

        Guid? pendingOperation = pendingStatus == ProvisioningAuthorityStatus.Authorized
            ? Guid.TryParseExact(record.AuthorizationId, "D", out var authorization) ? authorization : null
            : record.RecoveryId;
        if (pendingOperation is not { } operationId
            || await AuthorityStateReader.ReadApprovalAsync(store, operationId, cancellationToken) is not { } pending)
        {
            return AuthorityDecision.Reject("pending_approval_not_found");
        }

        if (pending.Approval.Status != AuthorityApprovalStatus.Issued || now <= pending.Approval.ExpiresAt)
        {
            return AuthorityDecision.Reject("pending_approval_still_active");
        }

        writes.Add(AuthorityStateReader.ApprovalWrite(pending.Approval with { Status = AuthorityApprovalStatus.Superseded }, pending.Version));
        return null;
    }

    private async Task<(AuthorityIssuanceHistory Document, long Version)?> ReadIssuanceAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        return AuthorityDocuments.FromItem<AuthorityIssuanceHistory>(
            await store.GetAsync(AuthorityStoreKeys.Issuance(deploymentId), cancellationToken),
            AuthorityDocuments.IssuanceType);
    }

    private static AuthorityStoreWrite IssuanceWrite(AuthorityIssuanceHistory history, long? expectedVersion)
    {
        return new AuthorityStoreWrite(
            AuthorityStoreKeys.Issuance(history.DeploymentId),
            AuthorityDocuments.ToItem(history, AuthorityDocuments.IssuanceType, (expectedVersion ?? 0) + 1),
            expectedVersion);
    }
}
