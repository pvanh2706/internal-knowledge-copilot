namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class TeamEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<UserEntity> Users { get; set; } = [];
}
