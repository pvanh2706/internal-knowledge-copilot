using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

namespace InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;

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
