using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.WorkflowCopilot;

public interface IWorkflowCopilotService
{
    Task<WorkflowRecommendationResponse> HandleDealStageChangedAsync(
        Guid userId,
        DealStageChangedWorkflowEventRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRecommendationResponse>> GetRecommendationsAsync(
        Guid userId,
        Guid? applicationId,
        string? objectType,
        string? externalObjectId,
        CancellationToken cancellationToken = default);

    Task<WorkflowRecommendationResponse> RecordRecommendationFeedbackAsync(
        Guid recommendationId,
        Guid userId,
        WorkflowRecommendationFeedbackRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowCopilotService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IKnowledgeKeywordIndexService keywordIndexService,
    IExternalObjectContextClient externalObjectContextClient,
    IExternalAccessResolver externalAccessResolver,
    IWorkflowRecommendationGenerationService recommendationGenerationService,
    IAuditLogService auditLogService) : IWorkflowCopilotService
{
    private const string DealStageChangedEventType = "deal.stage.changed";
    private const string DealObjectType = "deal";
    private const int SearchLimit = 30;
    private const int KeywordSearchLimit = 20;
    private const int MaxRecommendationSources = 8;
    private const int IdempotencyKeyMaxLength = 200;
    private const int ExternalObjectIdMaxLength = 300;
    private const int StageMaxLength = 200;
    private const int FeedbackNoteMaxLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<WorkflowRecommendationResponse> HandleDealStageChangedAsync(
        Guid userId,
        DealStageChangedWorkflowEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var application = await GetActiveApplicationAsync(tenantId, request.ApplicationId, cancellationToken);
        var externalObjectId = CleanRequired(request.ExternalObjectId, "external_object_id_required", ExternalObjectIdMaxLength);
        var toStage = CleanRequired(request.ToStage, "deal_stage_required", StageMaxLength);
        var fromStage = CleanOptional(request.FromStage, StageMaxLength);
        var idempotencyKey = BuildIdempotencyKey(request, application.Id, externalObjectId, toStage);

        var existingRecommendation = await GetExistingRecommendationAsync(tenantId, application.Id, idempotencyKey, cancellationToken);
        if (existingRecommendation is not null)
        {
            return ToResponse(existingRecommendation);
        }

        var workflow = await ResolveWorkflowDefinitionAsync(
            tenantId,
            application.Id,
            DealStageChangedEventType,
            DealObjectType,
            toStage,
            cancellationToken);
        await EnsureExternalObjectAccessAsync(userId, tenantId, application.Id, DealObjectType, externalObjectId, cancellationToken);

        var objectContextJson = HasInlineContext(request)
            ? BuildInlineObjectContextJson(request, externalObjectId, fromStage, toStage)
            : await FetchObjectContextAsync(tenantId, application.Id, DealObjectType, externalObjectId, fromStage, toStage, cancellationToken);

        var sources = await RetrieveWorkflowSourcesAsync(
            tenantId,
            application.Id,
            workflow,
            DealStageChangedEventType,
            DealObjectType,
            externalObjectId,
            toStage,
            objectContextJson,
            cancellationToken);
        var draft = await recommendationGenerationService.GenerateAsync(
            new WorkflowRecommendationGenerationRequest(
                workflow.Name,
                DealStageChangedEventType,
                DealObjectType,
                externalObjectId,
                toStage,
                objectContextJson,
                workflow.Steps
                    .OrderBy(step => step.StepOrder)
                    .Select(step => new WorkflowRecommendationStepContext(step.Name, step.Instruction, step.RetrievalQuery))
                    .ToList(),
                sources
                    .Select(source => new WorkflowRecommendationSourceContext(
                        KnowledgeSourceTypeMetadata.ToMetadataValue(source.SourceType),
                        source.SourceId,
                        source.Title,
                        source.SectionTitle,
                        source.Excerpt))
                    .ToList()),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var domainEvent = new DomainEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = application.Id,
            WorkflowDefinitionId = workflow.Id,
            EventType = DealStageChangedEventType,
            ObjectType = DealObjectType,
            ExternalObjectId = externalObjectId,
            IdempotencyKey = idempotencyKey,
            OccurredAt = request.OccurredAt ?? now,
            PayloadJson = JsonSerializer.Serialize(request, JsonOptions),
            ObjectContextJson = objectContextJson,
            Status = DomainEventStatus.RecommendationCreated,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var recommendation = new AiRecommendationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = application.Id,
            DomainEventId = domainEvent.Id,
            DomainEvent = domainEvent,
            WorkflowDefinitionId = workflow.Id,
            ObjectType = DealObjectType,
            ExternalObjectId = externalObjectId,
            Title = CleanGeneratedText(draft.Title, "Workflow recommendation", 300),
            Summary = CleanGeneratedText(draft.Summary, "Review event context and process sources.", 4000),
            RecommendedNextStepsJson = SerializeList(draft.RecommendedNextSteps),
            RisksJson = SerializeList(draft.Risks),
            ClarificationQuestionsJson = SerializeList(draft.ClarificationQuestions),
            SuggestedTasksJson = SerializeList(draft.SuggestedTasks),
            WarningsJson = SerializeList(draft.Warnings),
            WonLostSignalsJson = SerializeList(EnsureReasoningSignals(draft.WonLostSignals)),
            ReasoningLabel = EnsureReasoningLabel(draft.ReasoningLabel),
            SourcesJson = JsonSerializer.Serialize(sources, JsonOptions),
            Status = AiRecommendationStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.DomainEvents.Add(domainEvent);
        dbContext.AiRecommendations.Add(recommendation);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(domainEvent).State = EntityState.Detached;
            dbContext.Entry(recommendation).State = EntityState.Detached;
            existingRecommendation = await GetExistingRecommendationAsync(tenantId, application.Id, idempotencyKey, cancellationToken);
            if (existingRecommendation is not null)
            {
                return ToResponse(existingRecommendation);
            }

            throw;
        }

        await auditLogService.RecordAsync(
            userId,
            "WorkflowRecommendationCreated",
            "AiRecommendation",
            recommendation.Id,
            new { recommendation.ApplicationId, recommendation.ObjectType, recommendation.ExternalObjectId, recommendation.WorkflowDefinitionId, Sources = sources.Count },
            cancellationToken);

        return ToResponse(recommendation);
    }

    public async Task<IReadOnlyList<WorkflowRecommendationResponse>> GetRecommendationsAsync(
        Guid userId,
        Guid? applicationId,
        string? objectType,
        string? externalObjectId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var normalizedObjectType = NormalizeOptionalKey(objectType, lowerInvariant: true);
        var normalizedExternalObjectId = CleanOptional(externalObjectId, ExternalObjectIdMaxLength);

        if ((normalizedObjectType is null) != (normalizedExternalObjectId is null))
        {
            throw new ArgumentException("external_object_scope_incomplete");
        }

        if (normalizedObjectType is not null)
        {
            if (applicationId is null)
            {
                throw new ArgumentException("application_required_for_object_recommendations");
            }

            await EnsureExternalObjectAccessAsync(userId, tenantId, applicationId.Value, normalizedObjectType, normalizedExternalObjectId!, cancellationToken);
        }
        else
        {
            await EnsureCanListTenantWideRecommendationsAsync(userId, tenantId, cancellationToken);
        }

        var query = dbContext.AiRecommendations
            .AsNoTracking()
            .Where(recommendation => recommendation.TenantId == tenantId);

        if (applicationId is not null)
        {
            query = query.Where(recommendation => recommendation.ApplicationId == applicationId);
        }

        if (normalizedObjectType is not null)
        {
            query = query.Where(recommendation =>
                recommendation.ObjectType == normalizedObjectType &&
                recommendation.ExternalObjectId == normalizedExternalObjectId);
        }

        var recommendations = await query.ToListAsync(cancellationToken);
        return recommendations
            .OrderByDescending(recommendation => recommendation.CreatedAt)
            .Take(100)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<WorkflowRecommendationResponse> RecordRecommendationFeedbackAsync(
        Guid recommendationId,
        Guid userId,
        WorkflowRecommendationFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var recommendation = await dbContext.AiRecommendations
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == recommendationId,
                cancellationToken);
        if (recommendation is null)
        {
            throw new KeyNotFoundException("recommendation_not_found");
        }

        await EnsureExternalObjectAccessAsync(
            userId,
            tenantId,
            recommendation.ApplicationId,
            recommendation.ObjectType,
            recommendation.ExternalObjectId,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        recommendation.FeedbackValue = request.Value;
        recommendation.FeedbackNote = CleanOptional(request.Note, FeedbackNoteMaxLength);
        recommendation.FeedbackByUserId = userId;
        recommendation.FeedbackAt = now;
        recommendation.Status = AiRecommendationStatus.FeedbackReceived;
        recommendation.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.RecordAsync(
            userId,
            "WorkflowRecommendationFeedbackRecorded",
            "AiRecommendation",
            recommendation.Id,
            new { recommendation.FeedbackValue },
            cancellationToken);

        return ToResponse(recommendation);
    }

    private async Task<ApplicationEntity> GetActiveApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.Id == applicationId &&
                    item.Status == ApplicationStatus.Active &&
                    item.DeletedAt == null,
                cancellationToken);

        return application ?? throw new KeyNotFoundException("application_not_found");
    }

    private async Task<WorkflowDefinitionEntity> ResolveWorkflowDefinitionAsync(
        Guid tenantId,
        Guid applicationId,
        string eventType,
        string objectType,
        string stage,
        CancellationToken cancellationToken)
    {
        var normalizedStage = NormalizeStage(stage);
        var candidates = await dbContext.WorkflowDefinitions
            .Include(workflow => workflow.Steps)
            .Where(workflow =>
                workflow.TenantId == tenantId &&
                workflow.ApplicationId == applicationId &&
                workflow.Status == WorkflowDefinitionStatus.Active &&
                workflow.DeletedAt == null &&
                workflow.EventType == eventType &&
                workflow.ObjectType == objectType &&
                (workflow.TriggerStage == null || workflow.TriggerStage == normalizedStage))
            .ToListAsync(cancellationToken);

        var workflow = candidates
            .OrderByDescending(item => string.Equals(item.TriggerStage, normalizedStage, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefault();

        return workflow ?? throw new KeyNotFoundException("workflow_definition_not_found");
    }

    private async Task<string> FetchObjectContextAsync(
        Guid tenantId,
        Guid applicationId,
        string objectType,
        string externalObjectId,
        string? fromStage,
        string toStage,
        CancellationToken cancellationToken)
    {
        var connection = await dbContext.IntegrationConnections
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ApplicationId == applicationId &&
                item.Status == IntegrationConnectionStatus.Active &&
                item.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);
        if (connection is null || !Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new ArgumentException("object_context_required");
        }

        var response = await externalObjectContextClient.GetObjectContextAsync(
            new ExternalConnectorContext(
                tenantId,
                applicationId,
                baseUrl,
                connection.AuthMode,
                connection.SecretReference,
                ApiKey: null),
            objectType,
            externalObjectId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.ContextJson))
        {
            throw new ArgumentException("object_context_required");
        }

        var fields = CreateBaseContextFields(externalObjectId, fromStage, toStage);
        fields["sourceContext"] = ParseJsonElement(response.ContextJson, "invalid_object_context_json");
        if (!string.IsNullOrWhiteSpace(response.MetadataJson))
        {
            fields["metadata"] = ParseJsonElement(response.MetadataJson, "invalid_object_context_metadata_json");
        }

        return JsonSerializer.Serialize(fields, JsonOptions);
    }

    private async Task<IReadOnlyList<RecommendationSourceSnapshot>> RetrieveWorkflowSourcesAsync(
        Guid tenantId,
        Guid applicationId,
        WorkflowDefinitionEntity workflow,
        string eventType,
        string objectType,
        string externalObjectId,
        string stage,
        string objectContextJson,
        CancellationToken cancellationToken)
    {
        var queryText = BuildRetrievalQuery(workflow, eventType, objectType, externalObjectId, stage, objectContextJson);
        var filter = new KnowledgeQueryFilter
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            IncludeCompanyVisible = true,
            SourceTypes = ["document", "wiki"],
            Statuses = ["approved", "published"],
        };
        var embedding = await embeddingService.CreateEmbeddingAsync(queryText, cancellationToken);
        var vectorResults = await vectorStore.QueryAsync(embedding, SearchLimit, filter, cancellationToken);
        var keywordResults = await keywordIndexService.SearchAsync(TokenizeForSearch(queryText), KeywordSearchLimit, filter, cancellationToken);
        var merged = MergeResults(vectorResults, keywordResults);
        return merged
            .Select(result => ToRecommendationSource(result, tenantId, applicationId))
            .Where(source => source is not null)
            .Select(source => source!)
            .Take(MaxRecommendationSources)
            .Select((source, index) => source with { Rank = index + 1 })
            .ToList();
    }

    private async Task EnsureCanListTenantWideRecommendationsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == userId && item.DeletedAt == null && item.IsActive,
                cancellationToken);
        if (user is null || user.Role is not (UserRole.Admin or UserRole.Reviewer))
        {
            throw new UnauthorizedAccessException("recommendation_list_forbidden");
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
            .Where(item =>
                item.TenantId == tenantId &&
                item.ApplicationId == applicationId &&
                item.Status == IntegrationConnectionStatus.Active &&
                item.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);
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

    private async Task<AiRecommendationEntity?> GetExistingRecommendationAsync(
        Guid tenantId,
        Guid applicationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.AiRecommendations
            .AsNoTracking()
            .Include(recommendation => recommendation.DomainEvent)
            .Where(recommendation =>
                recommendation.TenantId == tenantId &&
                recommendation.ApplicationId == applicationId &&
                recommendation.DomainEvent != null &&
                recommendation.DomainEvent.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool HasInlineContext(DealStageChangedWorkflowEventRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.DealContextJson) ||
            !string.IsNullOrWhiteSpace(request.NotesJson) ||
            !string.IsNullOrWhiteSpace(request.TasksJson) ||
            !string.IsNullOrWhiteSpace(request.EmailsJson) ||
            !string.IsNullOrWhiteSpace(request.CallsJson) ||
            !string.IsNullOrWhiteSpace(request.RecentActivitiesJson);
    }

    private static string BuildInlineObjectContextJson(
        DealStageChangedWorkflowEventRequest request,
        string externalObjectId,
        string? fromStage,
        string toStage)
    {
        var fields = CreateBaseContextFields(externalObjectId, fromStage, toStage);
        AddJsonField(fields, "deal", request.DealContextJson, "invalid_deal_context_json");
        AddJsonField(fields, "notes", request.NotesJson, "invalid_notes_json");
        AddJsonField(fields, "tasks", request.TasksJson, "invalid_tasks_json");
        AddJsonField(fields, "emails", request.EmailsJson, "invalid_emails_json");
        AddJsonField(fields, "calls", request.CallsJson, "invalid_calls_json");
        AddJsonField(fields, "recentActivities", request.RecentActivitiesJson, "invalid_recent_activities_json");
        return JsonSerializer.Serialize(fields, JsonOptions);
    }

    private static Dictionary<string, object?> CreateBaseContextFields(string externalObjectId, string? fromStage, string toStage)
    {
        return new Dictionary<string, object?>
        {
            ["objectType"] = DealObjectType,
            ["externalObjectId"] = externalObjectId,
            ["fromStage"] = fromStage,
            ["toStage"] = toStage,
        };
    }

    private static void AddJsonField(Dictionary<string, object?> fields, string name, string? json, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        fields[name] = ParseJsonElement(json, errorCode);
    }

    private static JsonElement ParseJsonElement(string json, string errorCode)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new ArgumentException(errorCode);
        }
    }

    private static string BuildRetrievalQuery(
        WorkflowDefinitionEntity workflow,
        string eventType,
        string objectType,
        string externalObjectId,
        string stage,
        string objectContextJson)
    {
        var stepText = string.Join(
            ' ',
            workflow.Steps
                .OrderBy(step => step.StepOrder)
                .Select(step => $"{step.Name} {step.Instruction} {step.RetrievalQuery}"));
        return string.Join(
            ' ',
            [
                workflow.Name,
                workflow.Description ?? string.Empty,
                eventType,
                objectType,
                externalObjectId,
                stage,
                stepText,
                objectContextJson,
            ]);
    }

    private static IReadOnlyList<KnowledgeVectorSearchResult> MergeResults(
        IReadOnlyList<KnowledgeVectorSearchResult> vectorResults,
        IReadOnlyList<KnowledgeVectorSearchResult> keywordResults)
    {
        var merged = new List<KnowledgeVectorSearchResult>(vectorResults.Count + keywordResults.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in vectorResults.Concat(keywordResults))
        {
            var id = GetString(result.Metadata, "chunk_id") ?? result.Id;
            if (seenIds.Add(id))
            {
                merged.Add(result);
            }
        }

        return merged;
    }

    private static RecommendationSourceSnapshot? ToRecommendationSource(
        KnowledgeVectorSearchResult result,
        Guid tenantId,
        Guid applicationId)
    {
        if (!KnowledgeSourceTypeMetadata.TryParse(GetString(result.Metadata, "source_type"), out var sourceType) ||
            sourceType is not (KnowledgeSourceType.Document or KnowledgeSourceType.Wiki))
        {
            return null;
        }

        var metadataTenantId = GetGuid(result.Metadata, "tenant_id");
        if (metadataTenantId is not null && metadataTenantId != tenantId)
        {
            return null;
        }

        var metadataApplicationId = GetGuid(result.Metadata, "application_id");
        if (metadataApplicationId is not null && metadataApplicationId != applicationId)
        {
            return null;
        }

        var status = GetString(result.Metadata, "status");
        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("published", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sourceId = GetString(result.Metadata, "source_id");
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            sourceId = result.Id;
        }

        return new RecommendationSourceSnapshot(
            sourceType,
            sourceId,
            metadataApplicationId,
            GetGuid(result.Metadata, "knowledge_source_id"),
            GetGuid(result.Metadata, "document_id"),
            GetGuid(result.Metadata, "wiki_page_id"),
            NormalizeOptionalKey(GetString(result.Metadata, "external_object_type"), lowerInvariant: true),
            CleanOptional(GetString(result.Metadata, "external_object_id"), ExternalObjectIdMaxLength),
            GetString(result.Metadata, "title") ?? "Knowledge source",
            GetString(result.Metadata, "folder_path") ?? string.Empty,
            CleanOptional(GetString(result.Metadata, "section_title"), 300),
            GetInt(result.Metadata, "section_index"),
            ToExcerpt(result.Text),
            0);
    }

    private static WorkflowRecommendationResponse ToResponse(AiRecommendationEntity recommendation)
    {
        return new WorkflowRecommendationResponse(
            recommendation.Id,
            recommendation.TenantId,
            recommendation.ApplicationId,
            recommendation.DomainEventId,
            recommendation.WorkflowDefinitionId,
            recommendation.ObjectType,
            recommendation.ExternalObjectId,
            recommendation.Title,
            recommendation.Summary,
            DeserializeList(recommendation.RecommendedNextStepsJson),
            DeserializeList(recommendation.RisksJson),
            DeserializeList(recommendation.ClarificationQuestionsJson),
            DeserializeList(recommendation.SuggestedTasksJson),
            DeserializeList(recommendation.WarningsJson),
            EnsureReasoningSignals(DeserializeList(recommendation.WonLostSignalsJson)),
            EnsureReasoningLabel(recommendation.ReasoningLabel),
            DeserializeSources(recommendation.SourcesJson),
            recommendation.Status,
            recommendation.FeedbackValue,
            recommendation.FeedbackNote,
            recommendation.CreatedAt,
            recommendation.UpdatedAt);
    }

    private static string BuildIdempotencyKey(
        DealStageChangedWorkflowEventRequest request,
        Guid applicationId,
        string externalObjectId,
        string toStage)
    {
        var key = CleanOptional(request.IdempotencyKey, IdempotencyKeyMaxLength);
        if (key is not null)
        {
            return key;
        }

        var occurredAt = request.OccurredAt?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "now";
        return TrimToMax($"deal-stage:{applicationId:N}:{externalObjectId}:{NormalizeStage(toStage)}:{occurredAt}", IdempotencyKeyMaxLength);
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

    private static string SerializeList(IReadOnlyList<string> values)
    {
        return JsonSerializer.Serialize(CleanList(values), JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<WorkflowRecommendationSourceResponse> DeserializeSources(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var sources = JsonSerializer.Deserialize<RecommendationSourceSnapshot[]>(json, JsonOptions) ?? [];
            return sources
                .OrderBy(source => source.Rank)
                .Select(source => new WorkflowRecommendationSourceResponse(
                    source.SourceType,
                    source.SourceId,
                    source.ApplicationId,
                    source.KnowledgeSourceId,
                    source.DocumentId,
                    source.WikiPageId,
                    source.ExternalObjectType,
                    source.ExternalObjectId,
                    source.Title,
                    source.FolderPath,
                    source.SectionTitle,
                    source.SectionIndex,
                    source.Excerpt,
                    source.Rank))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> EnsureReasoningSignals(IReadOnlyList<string> signals)
    {
        var cleaned = CleanList(signals);
        if (cleaned.Length == 0)
        {
            return ["Reasoning-based, not predictive ML: no won/lost signal was produced from the provided context."];
        }

        return cleaned
            .Select(signal => signal.Contains("not predictive ML", StringComparison.OrdinalIgnoreCase)
                ? signal
                : $"Reasoning-based, not predictive ML: {signal}")
            .ToArray();
    }

    private static string EnsureReasoningLabel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            !value.Contains("not predictive ML", StringComparison.OrdinalIgnoreCase)
            ? "Reasoning-based signal, not predictive ML."
            : value.Trim();
    }

    private static string CleanGeneratedText(string? value, string fallback, int maxLength)
    {
        var cleaned = CleanOptional(value, maxLength);
        return cleaned ?? fallback;
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

    private static string? NormalizeOptionalKey(string? value, bool lowerInvariant)
    {
        var cleaned = CleanOptional(value, 300);
        if (cleaned is null)
        {
            return null;
        }

        return lowerInvariant ? cleaned.ToLowerInvariant() : cleaned;
    }

    private static string NormalizeStage(string stage)
    {
        return CleanRequired(stage, "deal_stage_required", StageMaxLength).ToLowerInvariant();
    }

    private static string TrimToMax(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string[] CleanList(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static Guid? GetGuid(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        var value = GetString(metadata, key);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static int? GetInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        var value = GetString(metadata, key);
        return int.TryParse(value, out var number) ? number : null;
    }

    private static string ToExcerpt(string text)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 360 ? normalized : normalized[..360].TrimEnd() + "...";
    }

    private static string[] TokenizeForSearch(string text)
    {
        return NormalizeForSearch(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static string NormalizeForSearch(string text)
    {
        var normalized = text
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record ExternalSubjectSet(string UserId, string? TeamId, string TenantId);

    private sealed record RecommendationSourceSnapshot(
        KnowledgeSourceType SourceType,
        string SourceId,
        Guid? ApplicationId,
        Guid? KnowledgeSourceId,
        Guid? DocumentId,
        Guid? WikiPageId,
        string? ExternalObjectType,
        string? ExternalObjectId,
        string Title,
        string FolderPath,
        string? SectionTitle,
        int? SectionIndex,
        string Excerpt,
        int Rank);
}
