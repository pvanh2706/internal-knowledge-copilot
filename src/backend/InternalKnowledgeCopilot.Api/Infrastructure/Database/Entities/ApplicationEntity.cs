using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class ApplicationEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public ApplicationType ApplicationType { get; set; } = ApplicationType.Internal;

    public string? BaseUrl { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
