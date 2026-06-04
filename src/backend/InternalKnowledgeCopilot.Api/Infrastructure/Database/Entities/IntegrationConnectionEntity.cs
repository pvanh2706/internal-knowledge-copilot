using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class IntegrationConnectionEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TenantEntity? Tenant { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationEntity? Application { get; set; }

    public required string Name { get; set; }

    public required string BaseUrl { get; set; }

    public IntegrationAuthMode AuthMode { get; set; } = IntegrationAuthMode.InternalApiKey;

    public IntegrationConnectionStatus Status { get; set; } = IntegrationConnectionStatus.Active;

    public required string SecretReference { get; set; }

    public string? SecretHash { get; set; }

    public DateTimeOffset? SecretRotatedAt { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
