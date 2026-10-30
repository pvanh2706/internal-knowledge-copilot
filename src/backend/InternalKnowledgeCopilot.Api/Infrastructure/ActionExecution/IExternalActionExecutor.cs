using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

namespace InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;

// Biên outbound write. Ở chế độ read-only (S01-E0) mọi implementation phải từ chối bằng ExternalActionBlockedException
// trước khi gửi bất kỳ request nào ra hệ thống nguồn.
public interface IExternalActionExecutor
{
    Task<ExternalActionValidationResponse> ValidateActionAsync(
        ExternalConnectorContext context,
        ExternalActionValidationRequest request,
        CancellationToken cancellationToken = default);

    Task<ExternalActionExecutionResponse> ExecuteActionAsync(
        ExternalConnectorContext context,
        ExternalActionExecutionRequest request,
        CancellationToken cancellationToken = default);
}
