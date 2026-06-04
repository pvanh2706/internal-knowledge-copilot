namespace InternalKnowledgeCopilot.Api.Common;

public enum AiActionRequestStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Executing,
    Succeeded,
    Failed,
    Cancelled
}
