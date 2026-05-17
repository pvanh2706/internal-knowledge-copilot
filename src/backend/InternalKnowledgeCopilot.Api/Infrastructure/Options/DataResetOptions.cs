namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class DataResetOptions
{
    public const string SectionName = "DataReset";

    public bool Enabled { get; init; }

    public string ConfirmationPhrase { get; init; } = "RESET TEST DATA";
}
