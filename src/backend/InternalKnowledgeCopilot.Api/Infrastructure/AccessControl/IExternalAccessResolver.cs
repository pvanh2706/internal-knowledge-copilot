using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;

public interface IExternalAccessResolver
{
    Task<ExternalAccessCheckResponse> CheckAccessAsync(
        ExternalConnectorContext context,
        ExternalAccessCheckRequest request,
        CancellationToken cancellationToken = default);
}
