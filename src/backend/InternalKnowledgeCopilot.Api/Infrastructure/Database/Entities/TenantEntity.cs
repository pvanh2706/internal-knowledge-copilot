using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class TenantEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Code { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<ApplicationEntity> Applications { get; set; } = [];
}
