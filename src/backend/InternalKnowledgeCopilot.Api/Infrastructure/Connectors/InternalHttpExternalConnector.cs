using System.Net.Http.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

public sealed class InternalHttpExternalConnector(HttpClient httpClient) :
    IExternalContentClient,
    IExternalObjectContextClient,
    IExternalAccessResolver,
    IExternalActionExecutor
{
    private const string IntegrationKeyHeaderName = "X-Integration-Key";
    private const string IntegrationKeyIdHeaderName = "X-Integration-Key-Id";

    public async Task<ExternalDocumentContentResponse> GetDocumentContentAsync(
        ExternalConnectorContext context,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(context, HttpMethod.Get, $"/copilot/documents/{Uri.EscapeDataString(externalId)}/content");
        return await SendAsync<ExternalDocumentContentResponse>(request, cancellationToken);
    }

    public async Task<ExternalObjectContextResponse> GetObjectContextAsync(
        ExternalConnectorContext context,
        string objectType,
        string externalObjectId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            context,
            HttpMethod.Get,
            $"/copilot/objects/{Uri.EscapeDataString(objectType)}/{Uri.EscapeDataString(externalObjectId)}/context");
        return await SendAsync<ExternalObjectContextResponse>(request, cancellationToken);
    }

    public async Task<ExternalAccessCheckResponse> CheckAccessAsync(
        ExternalConnectorContext context,
        ExternalAccessCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateRequest(context, HttpMethod.Post, "/copilot/permissions/check", request);
        return await SendAsync<ExternalAccessCheckResponse>(httpRequest, cancellationToken);
    }

    public async Task<ExternalActionValidationResponse> ValidateActionAsync(
        ExternalConnectorContext context,
        ExternalActionValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateRequest(context, HttpMethod.Post, "/copilot/actions/validate", request);
        return await SendAsync<ExternalActionValidationResponse>(httpRequest, cancellationToken);
    }

    public async Task<ExternalActionExecutionResponse> ExecuteActionAsync(
        ExternalConnectorContext context,
        ExternalActionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateRequest(context, HttpMethod.Post, "/copilot/actions/execute", request);
        return await SendAsync<ExternalActionExecutionResponse>(httpRequest, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(ExternalConnectorContext context, HttpMethod method, string relativePath, object? body = null)
    {
        var baseUri = context.BaseUrl.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? context.BaseUrl
            : new Uri(context.BaseUrl.AbsoluteUri + "/", UriKind.Absolute);
        var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath.TrimStart('/')));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (context.AuthMode == IntegrationAuthMode.InternalApiKey && !string.IsNullOrWhiteSpace(context.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(IntegrationKeyHeaderName, context.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(context.SecretReference))
        {
            request.Headers.TryAddWithoutValidation(IntegrationKeyIdHeaderName, context.SecretReference);
        }

        return request;
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
        return content ?? throw new InvalidOperationException("external_connector_empty_response");
    }
}
