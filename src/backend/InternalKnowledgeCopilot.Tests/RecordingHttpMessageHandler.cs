using System.Collections.Concurrent;
using System.Net;

namespace InternalKnowledgeCopilot.Tests;

// Ghi lại mọi request outbound để test chứng minh được không có request nào rời khỏi process.
public sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> requests = new();

    public IReadOnlyCollection<RecordedRequest> Requests => requests.ToArray();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri));
        return Task.FromResult(respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    public sealed record RecordedRequest(HttpMethod Method, Uri? Uri);
}
