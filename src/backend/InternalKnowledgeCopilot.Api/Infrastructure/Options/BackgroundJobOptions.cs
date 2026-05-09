namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public int PollSeconds { get; init; } = 5;

    public int MaxAttempts { get; init; } = 3;
}
