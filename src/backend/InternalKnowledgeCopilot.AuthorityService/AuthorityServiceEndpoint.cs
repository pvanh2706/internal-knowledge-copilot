using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.AuthorityService;

public sealed record AuthorityEndpointResponse(int StatusCode, AuthorityServiceResponse Body)
{
    public string ToJson() => JsonSerializer.Serialize(Body, AuthorityServiceJson.Options);
}

// Điểm vào duy nhất của Authority Service (adapter API Gateway → Lambda thuộc C-EX2). Thứ tự xử lý:
// 1) chỉ nhận lời gọi qua API Gateway với IAM auth, 2) xác minh principal qua registry do Security Operations quản lý,
// 3) route approval chỉ cho Approver, route operation chỉ cho Operator, 4) parse body chặt (không nhận trường danh tính),
// 5) replay theo ledger bền vững, 6) handler quyết định, 7) commit nguyên tử gồm ledger, 8) audit có cấu trúc.
public sealed class AuthorityServiceEndpoint(IAuthorityStore store, AuthorityServicePolicy policy, IAuthorityAuditSink audit, TimeProvider clock)
{
    private readonly AuthorityApprovalHandler approvals = new(store, policy);
    private readonly AuthorityTransitionHandler transitions = new(store, policy);

    public async Task<AuthorityEndpointResponse> HandleAsync(AuthorityInvocation invocation, CancellationToken cancellationToken = default)
    {
        var route = invocation.RouteKey;
        var context = new AuditContext(route);

        // Kiểm tra ngữ cảnh gateway trước khi đọc bất kỳ dữ liệu nào (lời gọi Lambda trực tiếp dừng ở đây).
        var preliminary = TrustedPrincipalResolver.Resolve(invocation.RequestContext, policy, registry: null);
        context.ObservedArn = preliminary.ObservedArn;
        if (preliminary.RejectReason != "principal_registry_unavailable")
        {
            return Reject(context, preliminary.RejectReason ?? "untrusted_invocation_context", 403);
        }

        PrincipalRegistryDocument registry;
        try
        {
            registry = await TrustedPrincipalResolver.LoadRegistryAsync(store, cancellationToken);
        }
        catch (AuthorityDocumentInvalidException)
        {
            return Reject(context, "principal_registry_unavailable", 503);
        }
        catch (AuthorityStoreUnavailableException ex)
        {
            return Reject(context, ex.Reason, 503);
        }

        var resolution = TrustedPrincipalResolver.Resolve(invocation.RequestContext, policy, registry);
        if (resolution.Principal is not { } caller)
        {
            return Reject(context, resolution.RejectReason!, 403);
        }

        context.Caller = caller;
        var isApprovalRoute = AuthorityServiceRoutes.ApproverRoutes.Contains(route);
        if (!isApprovalRoute && !AuthorityServiceRoutes.OperatorRoutes.Contains(route))
        {
            return Reject(context, "route_not_found", 404);
        }

        var requiredFunction = isApprovalRoute ? AuthorityFunction.Approver : AuthorityFunction.Operator;
        if (caller.Function != requiredFunction)
        {
            return Reject(context, isApprovalRoute ? "approver_role_required" : "operator_role_required", 403);
        }

        AuthorityApprovalRequest? approvalRequest = null;
        AuthorityOperationRequest? operationRequest = null;
        string canonical;
        try
        {
            if (isApprovalRoute)
            {
                approvalRequest = JsonSerializer.Deserialize<AuthorityApprovalRequest>(invocation.Body ?? string.Empty, AuthorityServiceJson.Options);
                canonical = JsonSerializer.Serialize(approvalRequest, AuthorityServiceJson.Options);
            }
            else
            {
                operationRequest = JsonSerializer.Deserialize<AuthorityOperationRequest>(invocation.Body ?? string.Empty, AuthorityServiceJson.Options);
                canonical = JsonSerializer.Serialize(operationRequest, AuthorityServiceJson.Options);
            }
        }
        catch (JsonException)
        {
            return Reject(context, "invalid_request", 400);
        }

        if (approvalRequest is null && operationRequest is null)
        {
            return Reject(context, "invalid_request", 400);
        }

        var requestId = approvalRequest?.RequestId ?? operationRequest!.RequestId;
        var deploymentId = approvalRequest?.DeploymentId ?? operationRequest!.DeploymentId;
        context.RequestId = requestId;
        context.DeploymentId = deploymentId;
        context.OperationId = operationRequest?.OperationId;
        if (requestId is null || requestId.Length < 8 || !ProvisioningAuthorityRecord.IsSafeIdentifier(requestId) || deploymentId == Guid.Empty)
        {
            return Reject(context, "invalid_request", 400);
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(route + "\n" + canonical)));
        try
        {
            // Idempotency dài hạn: cùng request_id + cùng nội dung + cùng người ⇒ trả kết quả cũ, không áp dụng lại.
            if (AuthorityDocuments.FromItem<AuthorityLedgerEntry>(await store.GetAsync(AuthorityStoreKeys.Ledger(requestId), cancellationToken), AuthorityDocuments.LedgerType) is { } existing)
            {
                var entry = existing.Document;
                if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal) || !string.Equals(entry.PersonId, caller.PersonId, StringComparison.Ordinal))
                {
                    return Reject(context, "request_id_conflict", 409);
                }

                context.OperationId = entry.OperationId;
                Emit(context, AuthorityAuditEventTypes.RequestReplayed, "replayed", entry.Reason, entry.ApproverPersonId, null, entry.AuthorityVersion);
                return new AuthorityEndpointResponse(200, Response(AuthorityResponseOutcome.Replayed, entry.Reason, context, entry.OperationId, entry.ApproverPersonId, entry.AuthorityVersion));
            }

            var now = AuthorityTimestamp.Truncate(clock.GetUtcNow());
            var decision = isApprovalRoute
                ? await approvals.DecideAsync(caller, approvalRequest!, registry, now, cancellationToken)
                : await transitions.DecideAsync(caller, route, operationRequest!, now, cancellationToken);
            if (!decision.Accepted)
            {
                return Reject(context, decision.Reason, decision.RejectStatusCode);
            }

            context.OperationId = decision.OperationId;
            var ledger = new AuthorityLedgerEntry
            {
                RequestId = requestId,
                Route = route,
                Fingerprint = fingerprint,
                PersonId = caller.PersonId,
                PrincipalArn = caller.RoleArn,
                DeploymentId = deploymentId,
                OperationId = decision.OperationId,
                Reason = decision.Reason,
                AuthorityVersion = decision.VersionAfter,
                ApproverPersonId = decision.ApproverPersonId,
                AppliedAt = now,
            };
            var writes = decision.Writes
                .Append(new AuthorityStoreWrite(AuthorityStoreKeys.Ledger(requestId), AuthorityDocuments.ToItem(ledger, AuthorityDocuments.LedgerType, 1), null))
                .ToList();
            await store.CommitAsync(requestId, writes, decision.Checks, cancellationToken);

            Emit(
                context,
                isApprovalRoute ? AuthorityAuditEventTypes.ApprovalIssued : AuthorityAuditEventTypes.TransitionApplied,
                "applied",
                decision.Reason,
                decision.ApproverPersonId,
                decision.VersionBefore,
                decision.VersionAfter);
            return new AuthorityEndpointResponse(200, Response(AuthorityResponseOutcome.Applied, decision.Reason, context, decision.OperationId, decision.ApproverPersonId, decision.VersionAfter));
        }
        catch (AuthorityStoreConflictException ex)
        {
            return Reject(context, ex.Reason, 409);
        }
        catch (AuthorityDocumentInvalidException ex)
        {
            return Reject(context, ex.Reason, 409);
        }
        catch (AuthorityStoreUnavailableException ex) when (ex.OutcomeUnknown)
        {
            Emit(context, AuthorityAuditEventTypes.OutcomeUnknown, "unknown", ex.Reason, null, null, null);
            return new AuthorityEndpointResponse(503, Response(AuthorityResponseOutcome.OutcomeUnknown, ex.Reason, context, context.OperationId, null, null));
        }
        catch (AuthorityStoreUnavailableException ex)
        {
            return Reject(context, ex.Reason, 503);
        }
    }

    private AuthorityEndpointResponse Reject(AuditContext context, string reason, int statusCode)
    {
        Emit(context, AuthorityAuditEventTypes.RequestRejected, "rejected", reason, null, null, null);
        return new AuthorityEndpointResponse(statusCode, Response(AuthorityResponseOutcome.Rejected, reason, context, context.OperationId, null, null));
    }

    private static AuthorityServiceResponse Response(
        AuthorityResponseOutcome outcome,
        string reason,
        AuditContext context,
        Guid? operationId,
        string? approverPersonId,
        long? authorityVersion)
    {
        return new AuthorityServiceResponse
        {
            Outcome = outcome,
            Reason = reason,
            RequestId = context.RequestId,
            OperationId = operationId,
            CallerPersonId = context.Caller?.PersonId,
            ApproverPersonId = approverPersonId,
            AuthorityVersion = authorityVersion,
        };
    }

    private void Emit(AuditContext context, string eventType, string outcome, string reason, string? approverPersonId, long? before, long? after)
    {
        audit.Emit(new AuthorityAuditEvent
        {
            EventType = eventType,
            OccurredAt = clock.GetUtcNow(),
            Outcome = outcome,
            Reason = reason,
            Route = context.Route,
            RequestId = context.RequestId,
            DeploymentId = context.DeploymentId,
            OperationId = context.OperationId,
            PersonId = context.Caller?.PersonId,
            Function = context.Caller?.Function.ToString(),
            PrincipalRoleArn = context.Caller?.RoleArn,
            ObservedPrincipalArn = context.ObservedArn,
            SessionName = context.Caller?.SessionName,
            ApproverPersonId = approverPersonId,
            AuthorityVersionBefore = before,
            AuthorityVersionAfter = after,
        });
    }

    private sealed class AuditContext(string route)
    {
        public string Route { get; } = route;

        public string? ObservedArn { get; set; }

        public TrustedPrincipal? Caller { get; set; }

        public string? RequestId { get; set; }

        public Guid? DeploymentId { get; set; }

        public Guid? OperationId { get; set; }
    }
}
