using System.Text.Json;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Audit;

public interface IAuditLogService
{
    Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService(AppDbContext dbContext, ITenantContext tenantContext) : IAuditLogService
{
    public async Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId ?? TenantDefaults.DefaultTenantId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
