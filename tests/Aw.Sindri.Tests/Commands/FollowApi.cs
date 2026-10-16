using System.IO.Pipelines;
using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The control plane: status bodies in turn (the last repeats), and an event stream the test writes to.</summary>
/// <param name="bodies">The status bodies.</param>
internal sealed class FollowApi(params string[] bodies) : HttpMessageHandler
{
    private readonly Pipe _stream = new();
    private readonly List<string?> _afters = [];
    private int _reads;
    private int _streams;

    /// <summary>Status reads answered with a body.</summary>
    public int StatusReads => Volatile.Read(ref _reads);

    /// <summary>Event streams asked for.</summary>
    public int Streams => Volatile.Read(ref _streams);

    public string? StreamQuery { get; private set; }

    /// <summary>The X-Sindri-After header of each status call, null for a plain one.</summary>
    public IReadOnlyList<string?> Afters
    {
        get
        {
            lock (_afters)
            {
                return [.. _afters];
            }
        }
    }

    public string? LastAfterHeader => Afters.Count == 0 ? null : Afters[^1];

    /// <summary>Every call fails as a stopped server does.</summary>
    public bool Down { get; set; }

    public bool StreamsFull { get; init; }

    /// <summary>A status call that asks to reflect a sequence is refused as SND-API-011 (views that never catch up).</summary>
    public bool ViewsBehind { get; init; }

    /// <summary>Every status call is refused as SND-API-005.</summary>
    public bool StatusFails { get; init; }

    public async Task Frame(long id, string type) => await Raw(Text(id, type));

    public static string Text(long id, string type) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"event: {type}\nid: {id}\ndata: {{}}\n\n");

    public async Task Raw(string text) => _ = await _stream.Writer.WriteAsync(Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);

    public async Task End() => await _stream.Writer.CompleteAsync();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath == "/sindri/events")
        {
            _ = Interlocked.Increment(ref _streams);
            StreamQuery = request.RequestUri.Query;
            return Task.FromResult(StreamsFull
                ? Problem(HttpStatusCode.TooManyRequests, "SND-API-012")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(_stream.Reader.AsStream()) { Headers = { ContentType = new("text/event-stream") } } });
        }

        if (Down)
        {
            throw new HttpRequestException("connection refused");
        }

        var after = request.Headers.TryGetValues("X-Sindri-After", out var values) ? string.Join(',', values) : null;
        lock (_afters)
        {
            _afters.Add(after);
        }

        if (StatusFails)
        {
            return Task.FromResult(Problem(HttpStatusCode.InternalServerError, "SND-API-005"));
        }

        if (ViewsBehind && after is not null)
        {
            return Task.FromResult(Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011"));
        }

        var body = bodies[Math.Min(Interlocked.Increment(ref _reads) - 1, bodies.Length - 1)];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private static HttpResponseMessage Problem(HttpStatusCode status, string code) => new(status)
    {
        Content = new StringContent(string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"title":"{{code}}","status":{{(int)status}},"detail":"refused"}"""), Encoding.UTF8, "application/problem+json"),
    };
}
