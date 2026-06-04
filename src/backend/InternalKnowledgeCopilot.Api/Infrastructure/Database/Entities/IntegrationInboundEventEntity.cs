using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class IntegrationInboundEventEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public Guid IntegrationConnectionId { get; set; }

    public IntegrationConnectionEntity? IntegrationConnection { get; set; }

    public IntegrationInboundEventType EventType { get; set; }

    public required string IdempotencyKey { get; set; }

    public string? ExternalEventId { get; set; }

    public string? ObjectType { get; set; }

    public string? ExternalObjectId { get; set; }

    public string? PayloadJson { get; set; }

    public IntegrationInboundEventStatus Status { get; set; } = IntegrationInboundEventStatus.Received;

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
