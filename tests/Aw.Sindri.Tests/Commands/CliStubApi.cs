using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Commands;

/// <summary>An HTTP handler that answers every call with one canned response and records each call's headers.</summary>
internal sealed class CliStubApi(HttpStatusCode status, string body) : HttpMessageHandler
{
    private readonly List<(string Route, Dictionary<string, string> Headers)> _calls = [];

    public IReadOnlyList<(string Route, Dictionary<string, string> Headers)> Calls => _calls;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _calls.Add((request.RequestUri!.AbsolutePath, request.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value), StringComparer.OrdinalIgnoreCase)));   // why: HttpClient always sets the URI
        var contentType = (int)status >= 400 ? "application/problem+json" : "application/json";
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) });
    }
}
