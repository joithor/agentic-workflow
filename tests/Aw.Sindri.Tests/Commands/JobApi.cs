using System.IO.Pipelines;
using System.Globalization;
using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The control plane for the job and service verbs: canned answers by 'METHOD /path' (a list answers in turn, the last repeating), the status
/// the read-your-writes verbs ask for first, and an event stream the test writes to.
/// </summary>
internal sealed class JobApi : HttpMessageHandler
{
    private readonly Pipe _stream = new();
    private readonly Dictionary<string, Reply[]> _replies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _served = new(StringComparer.Ordinal);
    private readonly List<string> _calls = [];
    private int _streams;

    /// <summary>Every call made, as 'METHOD /path?query'.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public int Streams => Volatile.Read(ref _streams);

    public string? StreamQuery { get; private set; }

    /// <summary>The event stream is full (429, SND-API-012).</summary>
    public bool StreamsFull { get; init; }

    /// <summary>Every call fails as a stopped server does.</summary>
    public bool Down { get; set; }

    public JobApi On(string method, string path, params Reply[] replies)
    {
        _replies[method + " " + path] = replies;
        return this;
    }

    public JobApi Ok(string method, string path, params string[] bodies) => On(method, path, [.. bodies.Select(static b => new Reply(HttpStatusCode.OK, b))]);

    public int Count(string call) => Calls.Count(c => string.Equals(c, call, StringComparison.Ordinal));

    public async Task Frame(long id, string type, string payload) =>
        _ = await _stream.Writer.WriteAsync(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"event: {type}\nid: {id}\ndata: {Event(id, type, payload)}\n\n")), TestContext.Current.CancellationToken);

    public async Task End() => await _stream.Writer.CompleteAsync();

    public static string Event(long id, string type, string payload) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"sequence":{{id}},"id":"00000000-0000-0000-0000-{{id:D12}}","type":"{{type}}","schemaVersion":1,"occurredAt":"2026-01-01T00:00:00.0000000Z","actor":"service:jobs","tenant":"local","ring":0,"item":null,"correlationId":"00000000-0000-0000-0000-000000000009","causationId":null,"traceId":null,"payload":{{payload}}}""");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;   // why: HttpClient always sets the URI
        lock (_calls)
        {
            _calls.Add(request.Method.Method + " " + uri.PathAndQuery);
        }

        if (uri.AbsolutePath == "/sindri/events")
        {
            _ = Interlocked.Increment(ref _streams);
            StreamQuery = uri.PathAndQuery;
            return Task.FromResult(StreamsFull
                ? Problem(HttpStatusCode.TooManyRequests, "SND-API-012")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(_stream.Reader.AsStream()) { Headers = { ContentType = new("text/event-stream") } } });
        }

        if (Down)
        {
            throw new HttpRequestException("connection refused");
        }

        var key = request.Method.Method + " " + uri.AbsolutePath;
        if (uri.AbsolutePath == "/sindri/status")
        {
            return Task.FromResult(Answer(new Reply(HttpStatusCode.OK, FollowRig.Status(needsApproval: false))));
        }

        return Task.FromResult(_replies.TryGetValue(key, out var replies) ? Answer(Next(key, replies)) : Problem(HttpStatusCode.NotFound, "SND-TEST-404"));
    }

    private Reply Next(string key, Reply[] replies)
    {
        lock (_served)
        {
            var n = _served.GetValueOrDefault(key);
            _served[key] = n + 1;
            return replies[Math.Min(n, replies.Length - 1)];
        }
    }

    private static HttpResponseMessage Answer(Reply reply) => new(reply.Status)
    {
        Content = new StringContent(reply.Body, Encoding.UTF8, (int)reply.Status >= 400 ? "application/problem+json" : "application/json"),
    };

    private static HttpResponseMessage Problem(HttpStatusCode status, string code) =>
        Answer(new Reply(status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"title":"{{code}}","status":{{(int)status}},"detail":"refused"}""")));
}
