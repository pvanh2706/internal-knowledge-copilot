using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class DomainEventEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }

    public WorkflowDefinitionEntity? WorkflowDefinition { get; set; }

    public Guid? IntegrationInboundEventId { get; set; }

    public IntegrationInboundEventEntity? IntegrationInboundEvent { get; set; }

    public required string EventType { get; set; }

    public required string ObjectType { get; set; }

    public required string ExternalObjectId { get; set; }

    public required string IdempotencyKey { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string? PayloadJson { get; set; }

    public string? ObjectContextJson { get; set; }

    public DomainEventStatus Status { get; set; } = DomainEventStatus.Received;

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
