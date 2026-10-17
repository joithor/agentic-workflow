using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An HTTP handler that answers each call with the next canned response and records each call's method, route and body.</summary>
internal sealed class ScriptedApi(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
{
    private readonly List<(string Method, string Route, string Body)> _calls = [];
    private int _next;

    public IReadOnlyList<(string Method, string Route, string Body)> Calls => _calls;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();   // as a real transport does: a cancelled token ends the call
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        _calls.Add((request.Method.Method, request.RequestUri!.PathAndQuery, body));   // why: HttpClient always sets the URI
        var (status, text) = answers[Math.Min(_next++, answers.Length - 1)];
        var contentType = (int)status >= 400 ? "application/problem+json" : "application/json";
        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, contentType) };
    }
}
