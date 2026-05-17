namespace InternalKnowledgeCopilot.Api.Modules.Admin;

public sealed record DataResetStatusResponse(
    bool Enabled,
    string ConfirmationPhrase,
    bool KeepsUsersTeamsAndAiSettings);

public sealed record DataResetRequest(
    string ConfirmationPhrase,
    bool ResetStorage,
    bool ResetVectorStore);

public sealed record DataResetResponse(
    DateTimeOffset CompletedAt,
    int DatabaseRowsDeleted,
    int StorageItemsDeleted,
    bool VectorStoreReset,
    bool UsersTeamsAndAiSettingsPreserved);
