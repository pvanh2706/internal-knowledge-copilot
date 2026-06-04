using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.ActionApprovals;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Integrations;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;

public sealed record ProcessingJobEnqueueRequest(
    Guid TenantId,
    Guid? ApplicationId,
    string JobType,
    string TargetType,
    Guid TargetId,
    string? IdempotencyKey = null,
    DateTimeOffset? ScheduledAt = null);

public interface IProcessingJobService
{
    Task<ProcessingJobEntity> EnqueueAsync(ProcessingJobEnqueueRequest request, CancellationToken cancellationToken = default);

    Task<ProcessingJobEntity?> ClaimNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> ProcessNextJobAsync(CancellationToken cancellationToken = default);
}

public sealed class ProcessingJobService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentProcessingService documentProcessingService,
    IAiFeedbackService feedbackService,
    IKnowledgeSourceService knowledgeSourceService,
    IKnowledgeIndexRebuildService knowledgeIndexRebuildService,
    IActionApprovalService actionApprovalService,
    IOptions<BackgroundJobOptions> options,
    ILogger<ProcessingJobService> logger) : IProcessingJobService
{
    private const int JobTypeMaxLength = 100;
    private const int TargetTypeMaxLength = 100;
    private const int IdempotencyKeyMaxLength = 200;
    private const int ErrorCodeMaxLength = 100;
    private const int ErrorTypeMaxLength = 300;
    private const int ErrorMessageMaxLength = 4000;
    private const int ErrorStackMaxLength = 8000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ProcessingJobEntity> EnqueueAsync(
        ProcessingJobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("tenant_id_required");
        }

        var jobType = CleanRequired(request.JobType, "job_type_required", JobTypeMaxLength);
        var targetType = CleanRequired(request.TargetType, "target_type_required", TargetTypeMaxLength);
        var idempotencyKey = CleanOptional(request.IdempotencyKey, IdempotencyKeyMaxLength);
        if (idempotencyKey is not null && request.ApplicationId is not null)
        {
            var existing = await FindExistingIdempotentJobAsync(
                request.TenantId,
                request.ApplicationId,
                jobType,
                idempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var job = new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            JobType = jobType,
            TargetType = targetType,
            TargetId = request.TargetId,
            IdempotencyKey = idempotencyKey,
            Status = ProcessingJobStatus.Pending,
            Attempts = 0,
            CreatedAt = now,
            ScheduledAt = request.ScheduledAt ?? now,
        };

        dbContext.ProcessingJobs.Add(job);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotencyKey is not null && request.ApplicationId is not null)
        {
            dbContext.Entry(job).State = EntityState.Detached;
            var existing = await FindExistingIdempotentJobAsync(
                request.TenantId,
                request.ApplicationId,
                jobType,
                idempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }

        return job;
    }

    public async Task<ProcessingJobEntity?> ClaimNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var pendingJobs = await dbContext.ProcessingJobs
            .Where(item => item.Status == ProcessingJobStatus.Pending)
            .ToListAsync(cancellationToken);
        var job = pendingJobs
            .Where(item => item.ScheduledAt <= now)
            .OrderBy(item => item.ScheduledAt)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        if (job is null)
        {
            return null;
        }

        var retryRule = GetRetryRule(job.JobType);
        if (job.Attempts >= retryRule.GetMaxAttempts(options.Value))
        {
            MarkDeadLettered(job, now, "max_attempts_exhausted", "Job reached maximum attempts before it could be claimed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        job.Status = ProcessingJobStatus.Running;
        job.Attempts += 1;
        job.StartedAt = now;
        job.LastAttemptAt = now;
        job.FinishedAt = null;
        job.DeadLetteredAt = null;
        job.ErrorMessage = null;
        job.ErrorCode = null;
        job.ErrorType = null;
        job.ErrorDetailsJson = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return job;
    }

    public async Task<bool> ProcessNextJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await ClaimNextDueJobAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await SetTenantAndApplicationContextAsync(job, cancellationToken);
            await DispatchAsync(job, cancellationToken);

            var finishedAt = DateTimeOffset.UtcNow;
            job.Status = ProcessingJobStatus.Succeeded;
            job.ErrorMessage = null;
            job.ErrorCode = null;
            job.ErrorType = null;
            job.ErrorDetailsJson = null;
            job.FinishedAt = finishedAt;
            job.DeadLetteredAt = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (ProcessingJobPermanentException ex)
        {
            await MarkFailedAsync(job, ex, permanent: true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(job, ex, permanent: false, cancellationToken);
            return true;
        }
    }

    private async Task DispatchAsync(ProcessingJobEntity job, CancellationToken cancellationToken)
    {
        if ((job.JobType == ProcessingJobTypes.DocumentSync ||
                job.JobType == ProcessingJobTypes.LegacyExtractAndEmbedDocument) &&
            job.TargetType == ProcessingJobTargetTypes.DocumentVersion)
        {
            await documentProcessingService.ProcessDocumentVersionAsync(job.TargetId, cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.ClassifyAiFailure &&
            job.TargetType == ProcessingJobTargetTypes.AiQualityIssue)
        {
            await feedbackService.ClassifyIssueAsync(job.TargetId, cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.ObjectSync &&
            job.TargetType == ProcessingJobTargetTypes.IntegrationInboundEvent)
        {
            await ProcessObjectSyncEventAsync(job, cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.PermissionSync &&
            job.TargetType == ProcessingJobTargetTypes.IntegrationInboundEvent)
        {
            await ProcessPermissionSyncEventAsync(job, cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.ActionExecution &&
            job.TargetType == ProcessingJobTargetTypes.AiActionRequest)
        {
            await ExecuteApprovedActionAsync(job, cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.IndexRebuild &&
            job.TargetType == ProcessingJobTargetTypes.Tenant)
        {
            if (job.TargetId != job.TenantId)
            {
                throw new ProcessingJobPermanentException("tenant_job_target_mismatch");
            }

            await knowledgeIndexRebuildService.RebuildAsync(null, new RebuildKnowledgeIndexRequest(), cancellationToken);
            return;
        }

        if (job.JobType == ProcessingJobTypes.WorkflowRecommendation)
        {
            throw new ProcessingJobPermanentException("workflow_recommendation_job_requires_payload");
        }

        throw new ProcessingJobPermanentException($"unsupported_processing_job:{job.JobType}/{job.TargetType}");
    }

    private async Task ProcessObjectSyncEventAsync(ProcessingJobEntity job, CancellationToken cancellationToken)
    {
        var inboundEvent = await GetInboundEventAsync(job, IntegrationInboundEventType.ObjectSync, cancellationToken);
        var request = DeserializePayload<ObjectSyncIntegrationRequest>(inboundEvent.PayloadJson, "invalid_object_sync_payload");
        var knowledgeSourceId = await ResolveKnowledgeSourceIdAsync(
            inboundEvent.TenantId,
            inboundEvent.ApplicationId,
            request.KnowledgeSourceExternalId,
            cancellationToken);
        var syncedAt = request.SyncedAt ?? DateTimeOffset.UtcNow;

        inboundEvent.Status = IntegrationInboundEventStatus.Processing;
        await dbContext.SaveChangesAsync(cancellationToken);

        await knowledgeSourceService.UpsertExternalObjectAsync(
            null,
            new UpsertExternalObjectRequest(
                inboundEvent.ApplicationId,
                knowledgeSourceId,
                request.ObjectType,
                request.ExternalObjectId,
                request.Title,
                request.Url,
                request.MetadataJson,
                request.ContentHash,
                request.AclHash,
                ExternalObjectStatus.Active,
                syncedAt,
                syncedAt,
                null),
            cancellationToken);

        inboundEvent.Status = IntegrationInboundEventStatus.Processed;
        inboundEvent.ProcessedAt = DateTimeOffset.UtcNow;
    }

    private async Task ProcessPermissionSyncEventAsync(ProcessingJobEntity job, CancellationToken cancellationToken)
    {
        var inboundEvent = await GetInboundEventAsync(job, IntegrationInboundEventType.PermissionSync, cancellationToken);
        var request = DeserializePayload<PermissionSyncIntegrationRequest>(inboundEvent.PayloadJson, "invalid_permission_sync_payload");

        inboundEvent.Status = IntegrationInboundEventStatus.Processing;
        await dbContext.SaveChangesAsync(cancellationToken);

        await knowledgeSourceService.ReplaceAclSnapshotsAsync(
            null,
            new ReplaceExternalAclSnapshotsRequest(
                inboundEvent.ApplicationId,
                request.ObjectType,
                request.ExternalObjectId,
                request.AclSnapshots
                    .Select(snapshot => new ExternalAclSnapshotItemRequest(
                        snapshot.SubjectType,
                        snapshot.SubjectId,
                        snapshot.SubjectDisplayName,
                        snapshot.Permission,
                        snapshot.ValidFrom,
                        snapshot.ValidTo,
                        snapshot.MetadataJson))
                    .ToList()),
            cancellationToken);

        inboundEvent.Status = IntegrationInboundEventStatus.Processed;
        inboundEvent.ProcessedAt = DateTimeOffset.UtcNow;
    }

    private async Task ExecuteApprovedActionAsync(ProcessingJobEntity job, CancellationToken cancellationToken)
    {
        var action = await dbContext.AiActionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == job.TenantId &&
                    item.ApplicationId == job.ApplicationId &&
                    item.Id == job.TargetId,
                cancellationToken);

        if (action is null)
        {
            throw new ProcessingJobPermanentException("action_request_not_found");
        }

        var executorUserId = action.ApprovedByUserId ?? action.RequestedByUserId ?? action.ExecutedByUserId;
        if (executorUserId is null)
        {
            throw new ProcessingJobPermanentException("action_executor_user_required");
        }

        await actionApprovalService.ExecuteActionAsync(action.Id, executorUserId.Value, cancellationToken);
    }

    private async Task<IntegrationInboundEventEntity> GetInboundEventAsync(
        ProcessingJobEntity job,
        IntegrationInboundEventType expectedEventType,
        CancellationToken cancellationToken)
    {
        var inboundEvent = await dbContext.IntegrationInboundEvents
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == job.TenantId &&
                    item.ApplicationId == job.ApplicationId &&
                    item.Id == job.TargetId,
                cancellationToken);

        if (inboundEvent is null)
        {
            throw new ProcessingJobPermanentException("inbound_event_not_found");
        }

        if (inboundEvent.EventType != expectedEventType)
        {
            throw new ProcessingJobPermanentException("inbound_event_type_mismatch");
        }

        return inboundEvent;
    }

    private async Task<Guid?> ResolveKnowledgeSourceIdAsync(
        Guid tenantId,
        Guid applicationId,
        string? externalSourceId,
        CancellationToken cancellationToken)
    {
        externalSourceId = CleanOptional(externalSourceId, 300);
        if (externalSourceId is null)
        {
            return null;
        }

        var source = await dbContext.KnowledgeSources
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.SourceType == KnowledgeSourceKind.External &&
                    item.ExternalSourceId == externalSourceId,
                cancellationToken);
        if (source is not null)
        {
            return source.Id;
        }

        var response = await knowledgeSourceService.UpsertSourceAsync(
            null,
            new UpsertKnowledgeSourceRequest(
                applicationId,
                KnowledgeSourceKind.External,
                externalSourceId,
                externalSourceId,
                KnowledgeSourceSyncMode.EventDriven,
                KnowledgeSourceStatus.Active,
                null,
                DateTimeOffset.UtcNow,
                null,
                "syncing",
                null),
            cancellationToken);
        return response.Id;
    }

    private async Task SetTenantAndApplicationContextAsync(ProcessingJobEntity job, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == job.TenantId && item.DeletedAt == null, cancellationToken);
        if (tenant is null)
        {
            throw new ProcessingJobPermanentException("tenant_not_found");
        }

        tenantContext.SetTenant(tenant.Id, tenant.Code);
        tenantContext.SetApplication(job.ApplicationId);

        if (job.ApplicationId is null)
        {
            return;
        }

        var applicationExists = await dbContext.Applications
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.TenantId == job.TenantId &&
                    item.Id == job.ApplicationId &&
                    item.DeletedAt == null &&
                    item.Status == ApplicationStatus.Active,
                cancellationToken);
        if (!applicationExists)
        {
            throw new ProcessingJobPermanentException("application_not_found");
        }
    }

    private async Task MarkFailedAsync(
        ProcessingJobEntity job,
        Exception exception,
        bool permanent,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var retryRule = GetRetryRule(job.JobType);
        var maxAttempts = retryRule.GetMaxAttempts(options.Value);
        var hasAttemptsRemaining = !permanent && job.Attempts < maxAttempts;
        job.ErrorMessage = Trim(exception.Message, ErrorMessageMaxLength);
        job.ErrorCode = ToErrorCode(exception.Message);
        job.ErrorType = Trim(exception.GetType().FullName ?? exception.GetType().Name, ErrorTypeMaxLength);
        job.ErrorDetailsJson = BuildErrorDetailsJson(exception);
        job.LastErrorAt = now;

        if (hasAttemptsRemaining)
        {
            job.Status = ProcessingJobStatus.Pending;
            job.ScheduledAt = now + retryRule.GetDelay(job.Attempts);
            job.FinishedAt = null;
            logger.LogWarning(
                exception,
                "Processing job {JobId} failed on attempt {Attempt}/{MaxAttempts}. Retrying at {ScheduledAt}.",
                job.Id,
                job.Attempts,
                maxAttempts,
                job.ScheduledAt);
        }
        else
        {
            MarkDeadLettered(job, now, job.ErrorCode ?? "processing_job_failed", job.ErrorMessage ?? "Processing job failed.");
            logger.LogWarning(
                exception,
                "Processing job {JobId} dead-lettered after {Attempt}/{MaxAttempts} attempts.",
                job.Id,
                job.Attempts,
                maxAttempts);

            if ((job.JobType == ProcessingJobTypes.DocumentSync ||
                    job.JobType == ProcessingJobTypes.LegacyExtractAndEmbedDocument) &&
                job.TargetType == ProcessingJobTargetTypes.DocumentVersion)
            {
                var version = await dbContext.DocumentVersions
                    .FirstOrDefaultAsync(item => item.TenantId == job.TenantId && item.Id == job.TargetId, cancellationToken);
                if (version is not null)
                {
                    version.Status = DocumentVersionStatus.ProcessingFailed;
                    version.UpdatedAt = now;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void MarkDeadLettered(ProcessingJobEntity job, DateTimeOffset now, string errorCode, string errorMessage)
    {
        job.Status = ProcessingJobStatus.DeadLettered;
        job.ErrorCode = Trim(errorCode, ErrorCodeMaxLength);
        job.ErrorMessage = Trim(errorMessage, ErrorMessageMaxLength);
        job.FinishedAt = now;
        job.DeadLetteredAt = now;
    }

    private async Task<ProcessingJobEntity?> FindExistingIdempotentJobAsync(
        Guid tenantId,
        Guid? applicationId,
        string jobType,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.ProcessingJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.ApplicationId == applicationId &&
                    item.JobType == jobType &&
                    item.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private static T DeserializePayload<T>(string? payloadJson, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ProcessingJobPermanentException(errorCode);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payloadJson, JsonOptions)
                ?? throw new ProcessingJobPermanentException(errorCode);
        }
        catch (JsonException ex)
        {
            throw new ProcessingJobPermanentException(errorCode, ex);
        }
    }

    private static ProcessingJobRetryRule GetRetryRule(string jobType)
    {
        return jobType switch
        {
            ProcessingJobTypes.DocumentSync or ProcessingJobTypes.LegacyExtractAndEmbedDocument
                => new ProcessingJobRetryRule(4, [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)]),
            ProcessingJobTypes.ObjectSync or ProcessingJobTypes.PermissionSync
                => new ProcessingJobRetryRule(4, [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)]),
            ProcessingJobTypes.WorkflowRecommendation
                => new ProcessingJobRetryRule(3, [TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(3)]),
            ProcessingJobTypes.ActionExecution
                => new ProcessingJobRetryRule(5, [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)]),
            ProcessingJobTypes.IndexRebuild
                => new ProcessingJobRetryRule(2, [TimeSpan.FromMinutes(5)]),
            ProcessingJobTypes.ClassifyAiFailure
                => new ProcessingJobRetryRule(3, [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]),
            _ => new ProcessingJobRetryRule(null, [TimeSpan.FromMinutes(1)])
        };
    }

    private static string BuildErrorDetailsJson(Exception exception)
    {
        return JsonSerializer.Serialize(
            new
            {
                exception = exception.GetType().FullName,
                exception.Message,
                innerException = exception.InnerException?.GetType().FullName,
                innerMessage = exception.InnerException?.Message,
                stackTrace = Trim(exception.StackTrace, ErrorStackMaxLength)
            },
            JsonOptions);
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string CleanRequired(string value, string errorCode, int maxLength)
    {
        return CleanOptional(value, maxLength) ?? throw new ArgumentException(errorCode);
    }

    private static string ToErrorCode(string? message)
    {
        var cleaned = CleanOptional(message, ErrorCodeMaxLength);
        if (cleaned is null)
        {
            return "processing_job_failed";
        }

        return cleaned.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or ':')
            ? cleaned
            : "processing_job_failed";
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record ProcessingJobRetryRule(int? MaxAttempts, IReadOnlyList<TimeSpan> Delays)
    {
        public int GetMaxAttempts(BackgroundJobOptions options)
        {
            return Math.Max(1, MaxAttempts ?? options.MaxAttempts);
        }

        public TimeSpan GetDelay(int attempts)
        {
            if (Delays.Count == 0)
            {
                return TimeSpan.FromMinutes(1);
            }

            return Delays[Math.Clamp(attempts - 1, 0, Delays.Count - 1)];
        }
    }

    private sealed class ProcessingJobPermanentException : Exception
    {
        public ProcessingJobPermanentException(string message)
            : base(message)
        {
        }

        public ProcessingJobPermanentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
