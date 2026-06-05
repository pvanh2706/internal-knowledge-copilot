using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.ActionApprovals;

public interface IActionApprovalService
{
    Task<AiActionRequestResponse> CreateActionAsync(
        Guid recommendationId,
        Guid userId,
        CreateAiActionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiActionRequestResponse>> GetActionsAsync(
        Guid userId,
        Guid? applicationId,
        AiActionRequestStatus? status,
        Guid? recommendationId,
        string? objectType,
        string? externalObjectId,
        CancellationToken cancellationToken = default);

    Task<AiActionRequestResponse> ApproveActionAsync(
        Guid actionRequestId,
        Guid userId,
        ApproveAiActionRequest request,
        CancellationToken cancellationToken = default);

    Task<AiActionRequestResponse> RejectActionAsync(
        Guid actionRequestId,
        Guid userId,
        RejectAiActionRequest request,
        CancellationToken cancellationToken = default);

    Task<AiActionRequestResponse> CancelActionAsync(
        Guid actionRequestId,
        Guid userId,
        CancelAiActionRequest request,
        CancellationToken cancellationToken = default);

    Task<AiActionRequestResponse> ExecuteActionAsync(
        Guid actionRequestId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IActionApprovalRuleService
{
    Task<ActionApprovalRuleDecision> EvaluateAsync(
        AiRecommendationEntity recommendation,
        string actionType,
        string targetObjectType,
        string targetExternalObjectId,
        string normalizedPayloadJson,
        CancellationToken cancellationToken = default);
}

public sealed class DefaultActionApprovalRuleService : IActionApprovalRuleService
{
    private static readonly HashSet<string> AutoApprovedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_task",
        "create_follow_up_task"
    };

    public Task<ActionApprovalRuleDecision> EvaluateAsync(
        AiRecommendationEntity recommendation,
        string actionType,
        string targetObjectType,
        string targetExternalObjectId,
        string normalizedPayloadJson,
        CancellationToken cancellationToken = default)
    {
        var approved = AutoApprovedActionTypes.Contains(actionType) &&
            string.Equals(targetObjectType, recommendation.ObjectType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(targetExternalObjectId, recommendation.ExternalObjectId, StringComparison.OrdinalIgnoreCase);
        var reason = approved
            ? "rule_auto_approved_safe_task_creation"
            : "rule_requires_manual_approval";

        return Task.FromResult(new ActionApprovalRuleDecision(approved, reason));
    }
}

public sealed class ActionApprovalService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IExternalAccessResolver externalAccessResolver,
    IExternalActionExecutor externalActionExecutor,
    IActionApprovalRuleService approvalRuleService,
    IAuditLogService auditLogService,
    ILogger<ActionApprovalService>? logger = null) : IActionApprovalService
{
    private const int ActionTypeMaxLength = 100;
    private const int ObjectTypeMaxLength = 100;
    private const int ExternalObjectIdMaxLength = 300;
    private const int IdempotencyKeyMaxLength = 200;
    private const int ReasonMaxLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AiActionRequestResponse> CreateActionAsync(
        Guid recommendationId,
        Guid userId,
        CreateAiActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureActiveUserAsync(userId, tenantId, cancellationToken);
        var recommendation = await GetRecommendationAsync(tenantId, recommendationId, cancellationToken);
        var actionType = NormalizeKey(request.ActionType, "action_type_required", ActionTypeMaxLength);
        var targetObjectType = NormalizeOptionalKey(request.TargetObjectType, ObjectTypeMaxLength) ?? recommendation.ObjectType;
        var targetExternalObjectId = CleanOptional(request.TargetExternalObjectId, ExternalObjectIdMaxLength) ?? recommendation.ExternalObjectId;
        var payloadJson = NormalizeJson(request.PayloadJson, "invalid_action_payload");
        var approvalMode = request.ApprovalMode ?? AiActionApprovalMode.Manual;
        var idempotencyKey = CleanOptional(request.IdempotencyKey, IdempotencyKeyMaxLength)
            ?? BuildIdempotencyKey(recommendation.Id, actionType, targetObjectType, targetExternalObjectId, payloadJson);

        var existing = await FindExistingActionAsync(tenantId, recommendation.ApplicationId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ToResponse(existing);
        }

        await EnsureExternalObjectAccessAsync(userId, tenantId, recommendation.ApplicationId, targetObjectType, targetExternalObjectId, cancellationToken);
        var validation = await ValidateActionWithSourceAsync(
            tenantId,
            recommendation.ApplicationId,
            actionType,
            targetObjectType,
            targetExternalObjectId,
            payloadJson,
            idempotencyKey,
            cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("action_validation_failed");
        }

        var normalizedPayloadJson = NormalizeJson(validation.NormalizedPayloadJson ?? payloadJson, "invalid_normalized_action_payload");
        var ruleDecision = approvalMode == AiActionApprovalMode.Rule
            ? await approvalRuleService.EvaluateAsync(
                recommendation,
                actionType,
                targetObjectType,
                targetExternalObjectId,
                normalizedPayloadJson,
                cancellationToken)
            : new ActionApprovalRuleDecision(false, "manual_approval_required");
        var now = DateTimeOffset.UtcNow;
        var status = request.CreateAsDraft
            ? AiActionRequestStatus.Draft
            : ruleDecision.IsApproved
                ? AiActionRequestStatus.Approved
                : AiActionRequestStatus.PendingApproval;
        var action = new AiActionRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = recommendation.ApplicationId,
            RecommendationId = recommendation.Id,
            ActionType = actionType,
            TargetObjectType = targetObjectType,
            TargetExternalObjectId = targetExternalObjectId,
            PayloadJson = payloadJson,
            NormalizedPayloadJson = normalizedPayloadJson,
            ApprovalMode = approvalMode,
            Status = status,
            IdempotencyKey = idempotencyKey,
            RequestedByUserId = userId,
            ApprovedAt = status == AiActionRequestStatus.Approved ? now : null,
            ValidationResultJson = SerializeValidation(validation),
            RuleDecisionJson = JsonSerializer.Serialize(ruleDecision, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.AiActionRequests.Add(action);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(action).State = EntityState.Detached;
            existing = await FindExistingActionAsync(tenantId, recommendation.ApplicationId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return ToResponse(existing);
            }

            throw;
        }

        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestCreated",
            "AiActionRequest",
            action.Id,
            new { action.RecommendationId, action.ActionType, action.TargetObjectType, action.TargetExternalObjectId, action.Status },
            cancellationToken);
        if (status == AiActionRequestStatus.Approved)
        {
            await auditLogService.RecordAsync(
                null,
                "AiActionRequestApprovedByRule",
                "AiActionRequest",
                action.Id,
                new { ruleDecision.Reason },
                cancellationToken);
        }

        return ToResponse(action);
    }

    public async Task<IReadOnlyList<AiActionRequestResponse>> GetActionsAsync(
        Guid userId,
        Guid? applicationId,
        AiActionRequestStatus? status,
        Guid? recommendationId,
        string? objectType,
        string? externalObjectId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var normalizedObjectType = NormalizeOptionalKey(objectType, ObjectTypeMaxLength);
        var normalizedExternalObjectId = CleanOptional(externalObjectId, ExternalObjectIdMaxLength);
        if ((normalizedObjectType is null) != (normalizedExternalObjectId is null))
        {
            throw new ArgumentException("external_object_scope_incomplete");
        }

        if (recommendationId is not null)
        {
            var recommendation = await GetRecommendationAsync(tenantId, recommendationId.Value, cancellationToken);
            if (applicationId is not null && recommendation.ApplicationId != applicationId)
            {
                throw new ArgumentException("recommendation_application_mismatch");
            }

            await EnsureExternalObjectAccessAsync(
                userId,
                tenantId,
                recommendation.ApplicationId,
                recommendation.ObjectType,
                recommendation.ExternalObjectId,
                cancellationToken);
        }

        if (normalizedObjectType is not null)
        {
            if (applicationId is null)
            {
                throw new ArgumentException("application_required_for_object_actions");
            }

            await EnsureExternalObjectAccessAsync(userId, tenantId, applicationId.Value, normalizedObjectType, normalizedExternalObjectId!, cancellationToken);
        }
        else if (recommendationId is null)
        {
            await EnsureCanListTenantWideActionsAsync(userId, tenantId, cancellationToken);
        }

        var query = dbContext.AiActionRequests
            .AsNoTracking()
            .Where(action => action.TenantId == tenantId);
        if (applicationId is not null)
        {
            query = query.Where(action => action.ApplicationId == applicationId);
        }

        if (status is not null)
        {
            query = query.Where(action => action.Status == status);
        }

        if (recommendationId is not null)
        {
            query = query.Where(action => action.RecommendationId == recommendationId);
        }

        if (normalizedObjectType is not null)
        {
            query = query.Where(action =>
                action.TargetObjectType == normalizedObjectType &&
                action.TargetExternalObjectId == normalizedExternalObjectId);
        }

        var actions = await query.ToListAsync(cancellationToken);
        return actions
            .OrderByDescending(action => action.CreatedAt)
            .Take(100)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<AiActionRequestResponse> ApproveActionAsync(
        Guid actionRequestId,
        Guid userId,
        ApproveAiActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureActiveUserAsync(userId, tenantId, cancellationToken);
        var action = await GetActionAsync(tenantId, actionRequestId, cancellationToken);
        await EnsureExternalObjectAccessAsync(userId, tenantId, action.ApplicationId, action.TargetObjectType, action.TargetExternalObjectId, cancellationToken);

        if (action.Status is AiActionRequestStatus.Approved or AiActionRequestStatus.Succeeded)
        {
            return ToResponse(action);
        }

        if (action.Status is not (AiActionRequestStatus.Draft or AiActionRequestStatus.PendingApproval or AiActionRequestStatus.Failed))
        {
            throw new InvalidOperationException("action_cannot_be_approved");
        }

        var validation = await ValidateActionWithSourceAsync(
            tenantId,
            action.ApplicationId,
            action.ActionType,
            action.TargetObjectType,
            action.TargetExternalObjectId,
            action.NormalizedPayloadJson ?? action.PayloadJson,
            action.IdempotencyKey,
            cancellationToken);
        action.ValidationResultJson = SerializeValidation(validation);
        if (!validation.IsValid)
        {
            action.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("action_validation_failed");
        }

        var now = DateTimeOffset.UtcNow;
        action.NormalizedPayloadJson = NormalizeJson(validation.NormalizedPayloadJson ?? action.NormalizedPayloadJson ?? action.PayloadJson, "invalid_normalized_action_payload");
        action.Status = AiActionRequestStatus.Approved;
        action.ApprovedByUserId = userId;
        action.ApprovedAt = now;
        action.RejectedByUserId = null;
        action.RejectedAt = null;
        action.RejectionReason = null;
        action.ExecutionError = null;
        action.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestApproved",
            "AiActionRequest",
            action.Id,
            new { request.Note },
            cancellationToken);

        return ToResponse(action);
    }

    public async Task<AiActionRequestResponse> RejectActionAsync(
        Guid actionRequestId,
        Guid userId,
        RejectAiActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureActiveUserAsync(userId, tenantId, cancellationToken);
        var action = await GetActionAsync(tenantId, actionRequestId, cancellationToken);
        await EnsureExternalObjectAccessAsync(userId, tenantId, action.ApplicationId, action.TargetObjectType, action.TargetExternalObjectId, cancellationToken);

        if (action.Status == AiActionRequestStatus.Rejected)
        {
            return ToResponse(action);
        }

        if (action.Status is AiActionRequestStatus.Executing or AiActionRequestStatus.Succeeded or AiActionRequestStatus.Cancelled)
        {
            throw new InvalidOperationException("action_cannot_be_rejected");
        }

        var now = DateTimeOffset.UtcNow;
        action.Status = AiActionRequestStatus.Rejected;
        action.RejectedByUserId = userId;
        action.RejectedAt = now;
        action.RejectionReason = CleanRequired(request.Reason, "rejection_reason_required", ReasonMaxLength);
        action.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestRejected",
            "AiActionRequest",
            action.Id,
            new { action.RejectionReason },
            cancellationToken);

        return ToResponse(action);
    }

    public async Task<AiActionRequestResponse> CancelActionAsync(
        Guid actionRequestId,
        Guid userId,
        CancelAiActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureActiveUserAsync(userId, tenantId, cancellationToken);
        var action = await GetActionAsync(tenantId, actionRequestId, cancellationToken);
        await EnsureExternalObjectAccessAsync(userId, tenantId, action.ApplicationId, action.TargetObjectType, action.TargetExternalObjectId, cancellationToken);

        if (action.Status == AiActionRequestStatus.Cancelled)
        {
            return ToResponse(action);
        }

        if (action.Status is AiActionRequestStatus.Executing or AiActionRequestStatus.Succeeded)
        {
            throw new InvalidOperationException("action_cannot_be_cancelled");
        }

        var now = DateTimeOffset.UtcNow;
        action.Status = AiActionRequestStatus.Cancelled;
        action.CancellationReason = CleanOptional(request.Reason, ReasonMaxLength);
        action.CancelledAt = now;
        action.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestCancelled",
            "AiActionRequest",
            action.Id,
            new { action.CancellationReason },
            cancellationToken);

        return ToResponse(action);
    }

    public async Task<AiActionRequestResponse> ExecuteActionAsync(
        Guid actionRequestId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureActiveUserAsync(userId, tenantId, cancellationToken);
        var action = await GetActionAsync(tenantId, actionRequestId, cancellationToken);
        await EnsureExternalObjectAccessAsync(userId, tenantId, action.ApplicationId, action.TargetObjectType, action.TargetExternalObjectId, cancellationToken);

        if (action.Status == AiActionRequestStatus.Succeeded)
        {
            return ToResponse(action);
        }

        if (action.Status is not (AiActionRequestStatus.Approved or AiActionRequestStatus.Failed))
        {
            throw new InvalidOperationException("action_not_approved");
        }

        var validation = await ValidateActionWithSourceAsync(
            tenantId,
            action.ApplicationId,
            action.ActionType,
            action.TargetObjectType,
            action.TargetExternalObjectId,
            action.NormalizedPayloadJson ?? action.PayloadJson,
            action.IdempotencyKey,
            cancellationToken);
        action.ValidationResultJson = SerializeValidation(validation);
        if (!validation.IsValid)
        {
            await MarkFailedAsync(action, validation.Reason ?? "action_validation_failed", userId, cancellationToken);
            throw new InvalidOperationException("action_validation_failed");
        }

        var now = DateTimeOffset.UtcNow;
        action.Status = AiActionRequestStatus.Executing;
        action.ExecutedByUserId = userId;
        action.ExecutingStartedAt = now;
        action.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestExecuting",
            "AiActionRequest",
            action.Id,
            new { action.ActionType, action.TargetObjectType, action.TargetExternalObjectId },
            cancellationToken);

        ExternalActionExecutionResponse execution;
        try
        {
            execution = await ExecuteWithSourceAsync(tenantId, action, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(action, ex.Message, userId, cancellationToken);
            throw new InvalidOperationException("action_execution_failed", ex);
        }

        now = DateTimeOffset.UtcNow;
        action.ExternalExecutionId = CleanOptional(execution.ExternalExecutionId, ExternalObjectIdMaxLength);
        action.ExecutionResultJson = NormalizeOptionalJson(execution.ResultJson, "invalid_action_execution_result");
        action.ExecutionError = CleanOptional(execution.Error, ReasonMaxLength);
        action.ExecutedAt = now;
        action.UpdatedAt = now;
        action.Status = execution.Succeeded ? AiActionRequestStatus.Succeeded : AiActionRequestStatus.Failed;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.RecordAsync(
            userId,
            execution.Succeeded ? "AiActionRequestSucceeded" : "AiActionRequestFailed",
            "AiActionRequest",
            action.Id,
            new { execution.ExternalExecutionId, execution.Error },
            cancellationToken);
        logger?.LogInformation(
            "AI action request {ActionRequestId} execution completed for tenant {TenantId} application {ApplicationId} with status {Status}.",
            action.Id,
            tenantId,
            action.ApplicationId,
            action.Status);

        if (!execution.Succeeded)
        {
            throw new InvalidOperationException("action_execution_failed");
        }

        return ToResponse(action);
    }

    private async Task MarkFailedAsync(AiActionRequestEntity action, string error, Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        action.Status = AiActionRequestStatus.Failed;
        action.ExecutionError = CleanOptional(error, ReasonMaxLength) ?? "action_failed";
        action.ExecutedAt = now;
        action.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            userId,
            "AiActionRequestFailed",
            "AiActionRequest",
            action.Id,
            new { action.ExecutionError },
            cancellationToken);
    }

    private async Task<ExternalActionExecutionResponse> ExecuteWithSourceAsync(
        Guid tenantId,
        AiActionRequestEntity action,
        CancellationToken cancellationToken)
    {
        var context = await GetConnectorContextAsync(tenantId, action.ApplicationId, cancellationToken);
        return await externalActionExecutor.ExecuteActionAsync(
            context,
            new ExternalActionExecutionRequest(
                action.ActionType,
                action.TargetObjectType,
                action.TargetExternalObjectId,
                action.NormalizedPayloadJson ?? action.PayloadJson,
                action.IdempotencyKey),
            cancellationToken);
    }

    private async Task<ExternalActionValidationResponse> ValidateActionWithSourceAsync(
        Guid tenantId,
        Guid applicationId,
        string actionType,
        string targetObjectType,
        string targetExternalObjectId,
        string payloadJson,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var context = await GetConnectorContextAsync(tenantId, applicationId, cancellationToken);
        try
        {
            return await externalActionExecutor.ValidateActionAsync(
                context,
                new ExternalActionValidationRequest(
                    actionType,
                    targetObjectType,
                    targetExternalObjectId,
                    payloadJson,
                    idempotencyKey),
                cancellationToken);
        }
        catch
        {
            return new ExternalActionValidationResponse(false, "action_validation_failed", null);
        }
    }

    private async Task<ExternalConnectorContext> GetConnectorContextAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.IntegrationConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.Status == IntegrationConnectionStatus.Active &&
                    item.DeletedAt == null,
                cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("action_connector_required");
        }

        if (!Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new InvalidOperationException("action_connector_invalid");
        }

        return new ExternalConnectorContext(
            tenantId,
            applicationId,
            baseUrl,
            connection.AuthMode,
            connection.SecretReference,
            ApiKey: null);
    }

    private async Task<AiRecommendationEntity> GetRecommendationAsync(Guid tenantId, Guid recommendationId, CancellationToken cancellationToken)
    {
        var recommendation = await dbContext.AiRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == recommendationId,
                cancellationToken);
        return recommendation ?? throw new KeyNotFoundException("recommendation_not_found");
    }

    private async Task<AiActionRequestEntity> GetActionAsync(Guid tenantId, Guid actionRequestId, CancellationToken cancellationToken)
    {
        var action = await dbContext.AiActionRequests
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == actionRequestId,
                cancellationToken);
        return action ?? throw new KeyNotFoundException("action_request_not_found");
    }

    private async Task<AiActionRequestEntity?> FindExistingActionAsync(
        Guid tenantId,
        Guid applicationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.AiActionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private async Task EnsureActiveUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId && item.Id == userId && item.DeletedAt == null && item.IsActive,
                cancellationToken);
        if (!exists)
        {
            throw new UnauthorizedAccessException("user_forbidden");
        }
    }

    private async Task EnsureCanListTenantWideActionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == userId && item.DeletedAt == null && item.IsActive,
                cancellationToken);
        if (user is null || user.Role is not (UserRole.Admin or UserRole.Reviewer))
        {
            throw new UnauthorizedAccessException("action_list_forbidden");
        }
    }

    private async Task EnsureExternalObjectAccessAsync(
        Guid userId,
        Guid tenantId,
        Guid applicationId,
        string objectType,
        string externalObjectId,
        CancellationToken cancellationToken)
    {
        var externalObject = await dbContext.ExternalObjects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.ObjectType == objectType &&
                    item.ExternalObjectId == externalObjectId &&
                    item.Status == ExternalObjectStatus.Active &&
                    item.DeletedAt == null,
                cancellationToken);
        if (externalObject is null)
        {
            return;
        }

        var subjectSet = await GetExternalSubjectSetAsync(userId, tenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var snapshots = await dbContext.ExternalAclSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.TenantId == tenantId &&
                snapshot.ApplicationId == applicationId &&
                snapshot.ExternalObjectRecordId == externalObject.Id &&
                snapshot.ObjectType == objectType &&
                snapshot.ExternalObjectId == externalObjectId)
            .ToListAsync(cancellationToken);

        if (!snapshots
            .Where(snapshot =>
                (snapshot.ValidFrom == null || snapshot.ValidFrom <= now) &&
                (snapshot.ValidTo == null || snapshot.ValidTo >= now))
            .Any(snapshot => IsAclSnapshotAllowed(snapshot, subjectSet, ExternalAclPermission.View)))
        {
            throw new UnauthorizedAccessException("external_object_forbidden");
        }

        var connection = await dbContext.IntegrationConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.Status == IntegrationConnectionStatus.Active &&
                    item.DeletedAt == null,
                cancellationToken);
        if (connection is null)
        {
            return;
        }

        if (!Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new UnauthorizedAccessException("external_object_forbidden");
        }

        ExternalAccessCheckResponse accessResponse;
        try
        {
            accessResponse = await externalAccessResolver.CheckAccessAsync(
                new ExternalConnectorContext(
                    tenantId,
                    applicationId,
                    baseUrl,
                    connection.AuthMode,
                    connection.SecretReference,
                    ApiKey: null),
                new ExternalAccessCheckRequest(
                    objectType,
                    externalObjectId,
                    "user",
                    userId.ToString(),
                    ExternalAclPermission.View),
                cancellationToken);
        }
        catch
        {
            throw new UnauthorizedAccessException("external_object_forbidden");
        }

        if (!accessResponse.IsAllowed)
        {
            throw new UnauthorizedAccessException("external_object_forbidden");
        }
    }

    private async Task<ExternalSubjectSet> GetExternalSubjectSetAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == userId && item.DeletedAt == null && item.IsActive)
            .Select(item => new { item.PrimaryTeamId })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("external_object_forbidden");
        }

        return new ExternalSubjectSet(userId.ToString(), user.PrimaryTeamId?.ToString(), tenantId.ToString());
    }

    private static bool IsAclSnapshotAllowed(
        ExternalAclSnapshotEntity snapshot,
        ExternalSubjectSet subjectSet,
        ExternalAclPermission requiredPermission)
    {
        return PermissionAllows(snapshot.Permission, requiredPermission) && SubjectMatches(snapshot, subjectSet);
    }

    private static bool PermissionAllows(ExternalAclPermission actual, ExternalAclPermission required)
    {
        return required switch
        {
            ExternalAclPermission.View => actual is ExternalAclPermission.View or ExternalAclPermission.Edit or ExternalAclPermission.Owner,
            ExternalAclPermission.Edit => actual is ExternalAclPermission.Edit or ExternalAclPermission.Owner,
            ExternalAclPermission.Owner => actual == ExternalAclPermission.Owner,
            _ => false,
        };
    }

    private static bool SubjectMatches(ExternalAclSnapshotEntity snapshot, ExternalSubjectSet subjectSet)
    {
        var subjectType = snapshot.SubjectType.Trim().ToLowerInvariant();
        return subjectType switch
        {
            "user" => string.Equals(snapshot.SubjectId, subjectSet.UserId, StringComparison.OrdinalIgnoreCase),
            "team" => !string.IsNullOrWhiteSpace(subjectSet.TeamId) &&
                string.Equals(snapshot.SubjectId, subjectSet.TeamId, StringComparison.OrdinalIgnoreCase),
            "tenant" => string.Equals(snapshot.SubjectId, subjectSet.TenantId, StringComparison.OrdinalIgnoreCase),
            "everyone" => snapshot.SubjectId == "*",
            _ => false,
        };
    }

    private static string BuildIdempotencyKey(
        Guid recommendationId,
        string actionType,
        string targetObjectType,
        string targetExternalObjectId,
        string payloadJson)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{recommendationId:N}:{actionType}:{targetObjectType}:{targetExternalObjectId}:{payloadJson}"));
        return $"action:{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
    }

    private static string SerializeValidation(ExternalActionValidationResponse validation)
    {
        return JsonSerializer.Serialize(validation, JsonOptions);
    }

    private static string NormalizeJson(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorCode);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            throw new ArgumentException(errorCode);
        }
    }

    private static string? NormalizeOptionalJson(string? value, string errorCode)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeJson(value, errorCode);
    }

    private static string NormalizeKey(string? value, string errorCode, int maxLength)
    {
        return CleanRequired(value, errorCode, maxLength).ToLowerInvariant();
    }

    private static string? NormalizeOptionalKey(string? value, int maxLength)
    {
        var cleaned = CleanOptional(value, maxLength);
        return cleaned?.ToLowerInvariant();
    }

    private static string CleanRequired(string? value, string errorCode, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorCode);
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException($"{errorCode}_too_long");
        }

        return cleaned;
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException("value_too_long");
        }

        return cleaned;
    }

    private static AiActionRequestResponse ToResponse(AiActionRequestEntity action)
    {
        return new AiActionRequestResponse(
            action.Id,
            action.TenantId,
            action.ApplicationId,
            action.RecommendationId,
            action.ActionType,
            action.TargetObjectType,
            action.TargetExternalObjectId,
            action.PayloadJson,
            action.NormalizedPayloadJson,
            action.ApprovalMode,
            action.Status,
            action.IdempotencyKey,
            action.RequestedByUserId,
            action.ApprovedByUserId,
            action.RejectedByUserId,
            action.ExecutedByUserId,
            action.RejectionReason,
            action.CancellationReason,
            action.ValidationResultJson,
            action.RuleDecisionJson,
            action.ExternalExecutionId,
            action.ExecutionResultJson,
            action.ExecutionError,
            action.CreatedAt,
            action.UpdatedAt,
            action.ApprovedAt,
            action.RejectedAt,
            action.ExecutingStartedAt,
            action.ExecutedAt,
            action.CancelledAt);
    }

    private sealed record ExternalSubjectSet(string UserId, string? TeamId, string TenantId);
}

public sealed record ActionApprovalRuleDecision(bool IsApproved, string Reason);
