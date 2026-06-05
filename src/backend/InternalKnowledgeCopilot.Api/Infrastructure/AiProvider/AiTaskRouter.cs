using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public sealed record AiTaskRoute(
    AiTaskType TaskType,
    string ProviderName,
    string Model,
    string EmbeddingProviderName,
    string EmbeddingModel,
    Guid PromptTemplateId,
    int PromptTemplateVersion,
    string PromptHash,
    string PromptName,
    string PromptMetadataJson);

public interface IAiTaskRouter
{
    Task<AiTaskRoute> ResolveAsync(AiTaskType taskType, CancellationToken cancellationToken = default);
}

public sealed class AiTaskRouter(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAiProviderSettingsService settingsService) : IAiTaskRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiTaskRoute> ResolveAsync(AiTaskType taskType, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        var template = await GetOrCreateDefaultTemplateAsync(tenantId, taskType, cancellationToken);
        return new AiTaskRoute(
            taskType,
            options.Name,
            SelectModel(taskType, options),
            options.EmbeddingProviderName,
            options.EmbeddingModel,
            template.Id,
            template.Version,
            template.PromptHash,
            template.Name,
            template.MetadataJson ?? "{}");
    }

    private async Task<AiPromptTemplateEntity> GetOrCreateDefaultTemplateAsync(
        Guid tenantId,
        AiTaskType taskType,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.AiPromptTemplates
            .FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.TaskType == taskType &&
                    item.Status == AiPromptTemplateStatus.Active &&
                    item.IsDefault,
                cancellationToken);
        if (template is not null)
        {
            return template;
        }

        var defaults = BuildDefaultTemplate(taskType);
        var now = DateTimeOffset.UtcNow;
        template = new AiPromptTemplateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskType = taskType,
            Name = defaults.Name,
            Version = 1,
            SystemPrompt = defaults.SystemPrompt,
            UserPromptTemplate = defaults.UserPromptTemplate,
            PromptHash = ComputeHash(defaults.SystemPrompt, defaults.UserPromptTemplate),
            Status = AiPromptTemplateStatus.Active,
            IsDefault = true,
            MetadataJson = JsonSerializer.Serialize(new { defaults.Description }, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.AiPromptTemplates.Add(template);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(template).State = EntityState.Detached;
            var existing = await dbContext.AiPromptTemplates
                .FirstOrDefaultAsync(
                    item =>
                        item.TenantId == tenantId &&
                        item.TaskType == taskType &&
                        item.Status == AiPromptTemplateStatus.Active &&
                        item.IsDefault,
                    cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }

        return template;
    }

    private static string SelectModel(AiTaskType taskType, AiProviderOptions options)
    {
        return taskType switch
        {
            AiTaskType.QuestionAnswering or AiTaskType.WorkflowRecommendation => options.ChatModel,
            _ => string.IsNullOrWhiteSpace(options.FastModel) ? options.ChatModel : options.FastModel,
        };
    }

    private static DefaultPromptTemplate BuildDefaultTemplate(AiTaskType taskType)
    {
        return taskType switch
        {
            AiTaskType.WorkflowRecommendation => new DefaultPromptTemplate(
                "Workflow recommendation v1",
                "Ground CRM workflow recommendations only in the event context, workflow steps, and retrieved process sources. Won/lost signals must be reasoning-based and not predictive ML.",
                "Use workflow, event, object context, and process sources to return a concise JSON recommendation.",
                "Default prompt for CRM workflow recommendation generation."),
            AiTaskType.WikiDraft => new DefaultPromptTemplate(
                "Wiki draft v1",
                "Convert reviewed internal documents into concise Vietnamese wiki drafts without inventing unsupported facts.",
                "Use the supplied title and source text to produce a structured wiki draft.",
                "Default prompt for wiki draft generation."),
            AiTaskType.DocumentUnderstanding => new DefaultPromptTemplate(
                "Document understanding v1",
                "Extract document summary, type, language, entities, effective dates, and quality warnings from supplied normalized text.",
                "Analyze the supplied document title, normalized text, and sections.",
                "Default prompt for document understanding."),
            AiTaskType.FeedbackClassification => new DefaultPromptTemplate(
                "Feedback classification v1",
                "Classify incorrect AI feedback into root causes and recommended knowledge improvements.",
                "Use interaction, sources, and reviewer note to classify the failure.",
                "Default prompt for AI feedback classification."),
            AiTaskType.Evaluation => new DefaultPromptTemplate(
                "Evaluation v1",
                "Evaluate AI answers against tenant-scoped expected keywords and forbidden leakage terms.",
                "Score the answer and flag missing expected terms or forbidden cross-tenant terms.",
                "Default prompt metadata for evaluation runs."),
            _ => new DefaultPromptTemplate(
                "Question answering v1",
                "Answer in Vietnamese using only retrieved tenant-authorized sources. Do not invent facts. Ask for clarification when sources are insufficient.",
                "Use the question and retrieved context chunks to return a grounded JSON answer with citations.",
                "Default prompt for internal knowledge question answering."),
        };
    }

    private static string ComputeHash(string systemPrompt, string userPromptTemplate)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt + "\n---\n" + userPromptTemplate));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record DefaultPromptTemplate(
        string Name,
        string SystemPrompt,
        string UserPromptTemplate,
        string Description);
}
