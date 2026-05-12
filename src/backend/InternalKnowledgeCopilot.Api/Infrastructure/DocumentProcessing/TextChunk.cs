namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public sealed record TextChunk(
    int Index,
    string Text,
    string? SectionTitle = null,
    int? SectionIndex = null,
    int? StartOffset = null,
    int? EndOffset = null);
