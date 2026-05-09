namespace InternalKnowledgeCopilot.Api.Modules.AuditLogs;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? MetadataJson,
    DateTimeOffset CreatedAt);
