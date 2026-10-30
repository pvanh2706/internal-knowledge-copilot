using System.Net;
using System.Net.Http.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

namespace InternalKnowledgeCopilot.Tests.Connectors;

public sealed class InternalHttpExternalConnectorTests
{
    private static readonly ExternalConnectorContext Context = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new Uri("https://crm.example.local"),
        IntegrationAuthMode.InternalApiKey,
        "crm-key-id",
        "crm-secret");

    [Fact]
    public async Task ExecuteActionAsync_DirectCall_IsBlockedWithoutOutboundRequest()
    {
        // Source sẵn sàng trả thành công để chứng minh connector tự từ chối, không phụ thuộc phản hồi của source.
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(
            new ExternalActionExecutionResponse(true, "external-action-001", """{"created":true}""", null)));
        IExternalActionExecutor executor = new InternalHttpExternalConnector(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => executor.ExecuteActionAsync(
            Context,
            new ExternalActionExecutionRequest("create_task", "deal", "D-100", """{"title":"Follow up"}""", "action-001")));

        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ValidateActionAsync_DirectCall_IsBlockedWithoutOutboundRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(
            new ExternalActionValidationResponse(true, null, """{"title":"Follow up"}""")));
        IExternalActionExecutor executor = new InternalHttpExternalConnector(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<ExternalActionBlockedException>(() => executor.ValidateActionAsync(
            Context,
            new ExternalActionValidationRequest("create_task", "deal", "D-100", """{"title":"Follow up"}""", "action-001")));

        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReadOnlyCalls_StillReachSourceSystem()
    {
        var handler = new RecordingHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/copilot/permissions/check" => JsonResponse(new ExternalAccessCheckResponse(true, null, DateTimeOffset.UtcNow)),
            "/copilot/objects/deal/D-100/context" => JsonResponse(new ExternalObjectContextResponse("deal", "D-100", """{"stage":"proposal"}""", null)),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var connector = new InternalHttpExternalConnector(new HttpClient(handler));

        var access = await connector.CheckAccessAsync(
            Context,
            new ExternalAccessCheckRequest("deal", "D-100", "user", "user-1", ExternalAclPermission.View));
        var objectContext = await connector.GetObjectContextAsync(Context, "deal", "D-100");

        Assert.True(access.IsAllowed);
        Assert.Equal("D-100", objectContext.ExternalObjectId);
        Assert.Equal(
            new[] { "POST /copilot/permissions/check", "GET /copilot/objects/deal/D-100/context" },
            handler.Requests.Select(request => $"{request.Method} {request.Uri!.AbsolutePath}"));
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
