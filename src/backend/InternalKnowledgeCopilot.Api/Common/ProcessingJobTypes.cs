namespace InternalKnowledgeCopilot.Api.Common;

public static class ProcessingJobTypes
{
    public const string DocumentSync = "DocumentSync";
    public const string ObjectSync = "ObjectSync";
    public const string PermissionSync = "PermissionSync";
    public const string WorkflowRecommendation = "WorkflowRecommendation";
    public const string ActionExecution = "ActionExecution";
    public const string IndexRebuild = "IndexRebuild";
    public const string ClassifyAiFailure = "ClassifyAiFailure";

    public const string LegacyExtractAndEmbedDocument = "ExtractAndEmbedDocument";
}
