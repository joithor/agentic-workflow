using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Server;

/// <summary>An open event stream on a golden host: reads its lines one at a time, each within five seconds.</summary>
internal sealed class EventStreamClient : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly StreamReader _reader;

    private EventStreamClient(HttpResponseMessage response, StreamReader reader)
    {
        _response = response;
        _reader = reader;
    }

    /// <summary>Opens <paramref name="route"/> as the human (or with <paramref name="headers"/> when given) and returns once the headers arrive.</summary>
    public static async Task<EventStreamClient> Open(GoldenHost host, string route, params (string Name, string Value)[] headers)
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = headers.Any(static h => h.Name.StartsWith("X-Sindri", StringComparison.OrdinalIgnoreCase)) ? headers : [.. Human(host.Token.Value), .. headers];
        using var request = Get(route, caller);
        var response = await host.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return new EventStreamClient(response, new StreamReader(await response.Content.ReadAsStreamAsync(ct)));
    }

    /// <summary>The next line, or null when the stream has ended; fails the test when none comes within five seconds.</summary>
    public async Task<string?> Line()
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        return await _reader.ReadLineAsync(limit.Token);
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _response.Dispose();
        return ValueTask.CompletedTask;
    }
}
