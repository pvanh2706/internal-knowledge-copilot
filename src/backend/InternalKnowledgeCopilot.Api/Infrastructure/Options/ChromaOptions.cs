namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class ChromaOptions
{
    public const string SectionName = "Chroma";

    public string BaseUrl { get; init; } = "http://localhost:8000";

    public string Collection { get; init; } = "knowledge_chunks";
}
