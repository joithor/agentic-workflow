using System.Collections.Concurrent;

namespace Aw.Context.Tests.Onyx;

/// <summary>An in-memory HTTP handler: answers with a script and records what each request carried (no header values).</summary>
internal sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
{
    private readonly ConcurrentQueue<SeenRequest> _requests = new();

    public IReadOnlyList<SeenRequest> Requests => [.. _requests];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(new SeenRequest(request.Method, request.RequestUri, request.Headers.Contains("Authorization")));
        return answer(request, cancellationToken);
    }
}
