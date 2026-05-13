using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public sealed class RuntimeEmbeddingService(
    IAiProviderSettingsService settingsService,
    MockEmbeddingService mockService,
    OpenAiCompatibleEmbeddingService openAiService) : IEmbeddingService
{
    public int Dimension
    {
        get
        {
            var options = settingsService.GetCurrent();
            return UseMockEmbeddings(options.EmbeddingProviderName) ? mockService.Dimension : options.EmbeddingDimension;
        }
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        return UseMockEmbeddings(options.EmbeddingProviderName)
            ? await mockService.CreateEmbeddingAsync(text, cancellationToken)
            : await openAiService.CreateEmbeddingAsync(text, cancellationToken);
    }

    private static bool UseMockEmbeddings(string providerName)
    {
        return string.Equals(providerName, "mock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerName, "anthropic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerName, "claude", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RuntimeAnswerGenerationService(
    IAiProviderSettingsService settingsService,
    MockAnswerGenerationService mockService,
    OpenAiCompatibleAnswerGenerationService openAiService) : IAnswerGenerationService
{
    public async Task<AiAnswerDraft> GenerateAsync(
        string question,
        IReadOnlyList<RetrievedKnowledgeChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        return string.Equals(options.Name, "mock", StringComparison.OrdinalIgnoreCase)
            ? await mockService.GenerateAsync(question, chunks, cancellationToken)
            : await openAiService.GenerateAsync(question, chunks, cancellationToken);
    }
}

public sealed class RuntimeWikiDraftGenerationService(
    IAiProviderSettingsService settingsService,
    MockWikiDraftGenerationService mockService,
    OpenAiCompatibleWikiDraftGenerationService openAiService) : IWikiDraftGenerationService
{
    public async Task<WikiDraftContent> GenerateAsync(
        string title,
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        return string.Equals(options.Name, "mock", StringComparison.OrdinalIgnoreCase)
            ? await mockService.GenerateAsync(title, sourceText, cancellationToken)
            : await openAiService.GenerateAsync(title, sourceText, cancellationToken);
    }
}

public sealed class RuntimeDocumentUnderstandingService(
    IAiProviderSettingsService settingsService,
    MockDocumentUnderstandingService mockService,
    OpenAiCompatibleDocumentUnderstandingService openAiService) : IDocumentUnderstandingService
{
    public async Task<DocumentUnderstandingResult> AnalyzeAsync(
        string title,
        string normalizedText,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsService.GetCurrentAsync(cancellationToken);
        return string.Equals(options.Name, "mock", StringComparison.OrdinalIgnoreCase)
            ? await mockService.AnalyzeAsync(title, normalizedText, sections, cancellationToken)
            : await openAiService.AnalyzeAsync(title, normalizedText, sections, cancellationToken);
    }
}
