namespace InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

public interface IExternalContentClient
{
    Task<ExternalDocumentContentResponse> GetDocumentContentAsync(
        ExternalConnectorContext context,
        string externalId,
        CancellationToken cancellationToken = default);
}
