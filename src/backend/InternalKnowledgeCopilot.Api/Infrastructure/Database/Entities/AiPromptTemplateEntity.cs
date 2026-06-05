using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiPromptTemplateEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public AiTaskType TaskType { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    public required string SystemPrompt { get; set; }

    public required string UserPromptTemplate { get; set; }

    public required string PromptHash { get; set; }

    public AiPromptTemplateStatus Status { get; set; } = AiPromptTemplateStatus.Active;

    public bool IsDefault { get; set; }

    public string? MetadataJson { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public UserEntity? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
