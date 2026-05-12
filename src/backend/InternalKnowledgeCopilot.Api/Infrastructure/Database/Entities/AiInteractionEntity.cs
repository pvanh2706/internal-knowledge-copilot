using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class AiInteractionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserEntity? User { get; set; }

    public required string Question { get; set; }

    public required string Answer { get; set; }

    public AiScopeType ScopeType { get; set; }

    public Guid? ScopeFolderId { get; set; }

    public Guid? ScopeDocumentId { get; set; }

    public bool NeedsClarification { get; set; }

    public string Confidence { get; set; } = "low";

    public string? MissingInformationJson { get; set; }

    public string? ConflictsJson { get; set; }

    public string? SuggestedFollowUpsJson { get; set; }

    public int LatencyMs { get; set; }

    public int UsedWikiCount { get; set; }

    public int UsedDocumentCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<AiInteractionSourceEntity> Sources { get; set; } = [];
}
