namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public UserEntity? ActorUser { get; set; }

    public required string Action { get; set; }

    public required string EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
