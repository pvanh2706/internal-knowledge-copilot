using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class WorkflowDefinitionEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string EventType { get; set; }

    public required string ObjectType { get; set; }

    public string? TriggerStage { get; set; }

    public WorkflowDefinitionStatus Status { get; set; } = WorkflowDefinitionStatus.Active;

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<WorkflowStepEntity> Steps { get; set; } = [];
}
