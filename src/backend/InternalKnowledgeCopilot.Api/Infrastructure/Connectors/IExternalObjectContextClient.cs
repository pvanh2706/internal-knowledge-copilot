namespace InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

public interface IExternalObjectContextClient
{
    Task<ExternalObjectContextResponse> GetObjectContextAsync(
        ExternalConnectorContext context,
        string objectType,
        string externalObjectId,
        CancellationToken cancellationToken = default);
}
