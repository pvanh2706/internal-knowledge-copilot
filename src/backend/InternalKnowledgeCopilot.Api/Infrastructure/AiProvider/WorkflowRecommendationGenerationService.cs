using System.Text.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IWorkflowRecommendationGenerationService
{
    Task<WorkflowRecommendationDraft> GenerateAsync(
        WorkflowRecommendationGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class MockWorkflowRecommendationGenerationService : IWorkflowRecommendationGenerationService
{
    public Task<WorkflowRecommendationDraft> GenerateAsync(
        WorkflowRecommendationGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var stage = string.IsNullOrWhiteSpace(request.Stage) ? "current stage" : request.Stage.Trim();
        var firstStep = request.Steps.FirstOrDefault();
        var firstSource = request.Sources.FirstOrDefault();
        var title = $"Next steps for {request.ObjectType} {request.ExternalObjectId} at {stage}";
        var summary = request.Sources.Count == 0
            ? $"The {request.ObjectType} event was received, but no process document was retrieved for {stage}."
            : $"Based on event context and {request.Sources.Count} process source(s), focus the {request.ObjectType} follow-up on {stage} readiness.";

        var nextSteps = new List<string>
        {
            firstStep?.Instruction ?? $"Review the event context before moving forward from {stage}.",
            firstSource is null
                ? "Attach the relevant process document before acting on this recommendation."
                : $"Check the cited process source '{firstSource.Title}' before changing the deal plan.",
            "Confirm owner, timing, and customer-facing commitment before creating CRM actions."
        };

        return Task.FromResult(new WorkflowRecommendationDraft(
            title,
            summary,
            nextSteps,
            request.Sources.Count == 0
                ? ["Recommendation is not grounded by a retrieved process citation."]
                : ["Stage movement may be premature if required evidence is missing."],
            ["What customer evidence supports the current stage?", "Who owns the next customer follow-up?"],
            [$"Create a follow-up task for {stage} validation.", "Record missing evidence in the CRM notes."],
            request.Sources.Count == 0
                ? ["No process source was retrieved for this event."]
                : ["Verify CRM context against the cited process before execution."],
            [
                "Reasoning-based, not predictive ML: positive signal if next-step owner, customer need, and evidence are present.",
                "Reasoning-based, not predictive ML: risk signal if recent activity or decision criteria are missing."
            ],
            "Reasoning-based signal, not predictive ML."));
    }
}

public sealed class OpenAiCompatibleWorkflowRecommendationGenerationService(
    OpenAiCompatibleClient client,
    IAiProviderSettingsService settingsService) : IWorkflowRecommendationGenerationService
{
    private const int MaxContextCharacters = 8000;
    private const int MaxSourceCharacters = 1200;

    public async Task<WorkflowRecommendationDraft> GenerateAsync(
        WorkflowRecommendationGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = """
            You are Workflow Copilot for CRM and sales operations.
            Write in Vietnamese unless the event context is clearly not Vietnamese.
            Use only the workflow steps, event context, and retrieved process sources provided.
            Do not invent facts, dates, prices, commitments, owners, or CRM state.
            Won/lost signals must be reasoning-based observations, not predictive ML scores.
            Return JSON only with this schema:
            {
              "title": "string",
              "summary": "string",
              "recommendedNextSteps": ["string"],
              "risks": ["string"],
              "clarificationQuestions": ["string"],
              "suggestedTasks": ["string"],
              "warnings": ["string"],
              "wonLostSignals": ["string"],
              "reasoningLabel": "Reasoning-based signal, not predictive ML."
            }
            Keep each list concise. If process sources are insufficient, say what is missing.
            """;

        var stepBlocks = request.Steps
            .Select((step, index) => $"{index + 1}. {step.Name}: {step.Instruction} Retrieval: {step.RetrievalQuery ?? "-"}")
            .ToArray();
        var sourceBlocks = request.Sources
            .Select((source, index) => $"""
                [S{index + 1}]
                Type: {source.SourceType}
                Title: {source.Title}
                Section: {source.SectionTitle ?? "-"}
                Excerpt:
                {Trim(source.Excerpt, MaxSourceCharacters)}
                """)
            .ToArray();

        var userPrompt = $"""
            Workflow: {request.WorkflowName}
            Event: {request.EventType}
            Object: {request.ObjectType} / {request.ExternalObjectId}
            Stage: {request.Stage ?? "-"}

            Workflow steps:
            {string.Join("\n", stepBlocks)}

            Event and object context:
            {Trim(request.ObjectContextJson, MaxContextCharacters)}

            Process sources:
            {string.Join("\n\n", sourceBlocks)}

            Create a grounded recommendation. Cite process thinking only from these sources and context.
            """;

        var options = await settingsService.GetCurrentAsync(cancellationToken);
        var rawDraft = await client.CompleteAsync(systemPrompt, userPrompt, options, cancellationToken);
        if (TryParseDraft(rawDraft, out var draft))
        {
            return draft;
        }

        var repairPrompt = $"""
            The previous output was not valid JSON for the required schema.

            Previous output:
            {rawDraft}

            Re-read the original request and return valid JSON only.

            Original request:
            {userPrompt}
            """;

        var repairedDraft = await client.CompleteAsync(systemPrompt, repairPrompt, options, cancellationToken);
        return TryParseDraft(repairedDraft, out var repaired)
            ? repaired
            : BuildFallbackDraft(rawDraft);
    }

    private static bool TryParseDraft(string rawDraft, out WorkflowRecommendationDraft draft)
    {
        draft = null!;
        var json = ExtractJsonObject(rawDraft);
        if (json is null)
        {
            return false;
        }

        WorkflowRecommendationJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WorkflowRecommendationJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Title) || string.IsNullOrWhiteSpace(parsed.Summary))
        {
            return false;
        }

        draft = new WorkflowRecommendationDraft(
            parsed.Title.Trim(),
            parsed.Summary.Trim(),
            CleanList(parsed.RecommendedNextSteps),
            CleanList(parsed.Risks),
            CleanList(parsed.ClarificationQuestions),
            CleanList(parsed.SuggestedTasks),
            CleanList(parsed.Warnings),
            NormalizeWonLostSignals(parsed.WonLostSignals),
            NormalizeReasoningLabel(parsed.ReasoningLabel));
        return true;
    }

    private static WorkflowRecommendationDraft BuildFallbackDraft(string rawDraft)
    {
        return new WorkflowRecommendationDraft(
            "Workflow recommendation requires review",
            string.IsNullOrWhiteSpace(rawDraft)
                ? "AI provider did not return a usable workflow recommendation."
                : Trim(rawDraft, 1000),
            ["Review the event context and cited process documents manually."],
            ["AI provider output was not valid JSON."],
            ["Which workflow source should govern this event?"],
            ["Create a manual review task."],
            ["Recommendation could not be parsed from provider output."],
            ["Reasoning-based, not predictive ML: no automated won/lost inference was produced."],
            "Reasoning-based signal, not predictive ML.");
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string[] CleanList(string[]? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static string[] NormalizeWonLostSignals(string[]? values)
    {
        var signals = CleanList(values);
        if (signals.Length == 0)
        {
            return ["Reasoning-based, not predictive ML: no won/lost signal could be inferred from the provided context."];
        }

        return signals
            .Select(signal => signal.Contains("not predictive ML", StringComparison.OrdinalIgnoreCase)
                ? signal
                : $"Reasoning-based, not predictive ML: {signal}")
            .ToArray();
    }

    private static string NormalizeReasoningLabel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            !value.Contains("not predictive ML", StringComparison.OrdinalIgnoreCase)
            ? "Reasoning-based signal, not predictive ML."
            : value.Trim();
    }

    private static string Trim(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd() + "...";
    }

    private sealed record WorkflowRecommendationJson(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("recommendedNextSteps")] string[]? RecommendedNextSteps,
        [property: JsonPropertyName("risks")] string[]? Risks,
        [property: JsonPropertyName("clarificationQuestions")] string[]? ClarificationQuestions,
        [property: JsonPropertyName("suggestedTasks")] string[]? SuggestedTasks,
        [property: JsonPropertyName("warnings")] string[]? Warnings,
        [property: JsonPropertyName("wonLostSignals")] string[]? WonLostSignals,
        [property: JsonPropertyName("reasoningLabel")] string? ReasoningLabel);
}

public sealed record WorkflowRecommendationGenerationRequest(
    string WorkflowName,
    string EventType,
    string ObjectType,
    string ExternalObjectId,
    string? Stage,
    string ObjectContextJson,
    IReadOnlyList<WorkflowRecommendationStepContext> Steps,
    IReadOnlyList<WorkflowRecommendationSourceContext> Sources);

public sealed record WorkflowRecommendationStepContext(
    string Name,
    string Instruction,
    string? RetrievalQuery);

public sealed record WorkflowRecommendationSourceContext(
    string SourceType,
    string SourceId,
    string Title,
    string? SectionTitle,
    string Excerpt);

public sealed record WorkflowRecommendationDraft(
    string Title,
    string Summary,
    IReadOnlyList<string> RecommendedNextSteps,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> ClarificationQuestions,
    IReadOnlyList<string> SuggestedTasks,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> WonLostSignals,
    string ReasoningLabel);
