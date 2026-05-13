namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class RetrievalHintEntity
{
    public Guid Id { get; set; }

    public Guid CorrectionId { get; set; }

    public KnowledgeCorrectionEntity? Correction { get; set; }

    public required string HintText { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
