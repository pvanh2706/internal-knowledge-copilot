namespace InternalKnowledgeCopilot.Api.Modules.Dashboard;

public sealed record DashboardSummaryResponse(
    IReadOnlyList<NamedCountResponse> DocumentCounts,
    IReadOnlyList<NamedCountResponse> WikiCounts,
    int AiQuestionCount,
    int FeedbackCorrectCount,
    int FeedbackIncorrectCount,
    int IncorrectFeedbackPendingCount,
    IReadOnlyList<TopCitedSourceResponse> TopCitedSources);

public sealed record NamedCountResponse(string Name, int Count);

public sealed record TopCitedSourceResponse(
    string SourceType,
    string Title,
    string FolderPath,
    int Count);
