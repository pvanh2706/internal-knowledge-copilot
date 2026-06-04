using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class WorkflowStepEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinitionEntity? WorkflowDefinition { get; set; }

    public int StepOrder { get; set; }

    public required string Name { get; set; }

    public WorkflowStepType StepType { get; set; } = WorkflowStepType.Guidance;

    public required string Instruction { get; set; }

    public string? RetrievalQuery { get; set; }

    public string? RequiredContextJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
