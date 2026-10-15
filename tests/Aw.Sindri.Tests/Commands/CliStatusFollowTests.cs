using System.IO.Pipelines;
using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri status --follow' (API P5 unit H): the summary again each time it changes, off the event stream; exit 0 on Ctrl-C, 1 when the server stops.</summary>
public sealed class CliStatusFollowTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly ProfileHash _hash = ProfileHash.From("3f1c2a" + new string('0', 58));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly CancellationTokenSource _interrupt = new();

    public void Dispose()
    {
        _interrupt.Dispose();
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_reprints_when_status_changes()
    {
        var api = new Api(Status(needsApproval: true), Status(needsApproval: false));
        var run = Start(api, json: false, TimeProvider.System);
        await WaitUntil(() => api.Streams == 1);

        await api.Frame(2, "service.heartbeat");
        await WaitUntil(() => api.StatusReads == 2);
        await WaitUntil(() => _out.ToString().Contains("approved, hash", StringComparison.Ordinal));
        await _interrupt.CancelAsync();

        Assert.Equal(0, await Finished(run));
        var text = _out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("the profile needs your approval", text, StringComparison.Ordinal);
        Assert.Contains("\n\n  profile", text, StringComparison.Ordinal);   // a blank line between the two summaries
        Assert.Equal("?after=0", api.StreamQuery);
        Assert.Equal("2", api.LastAfterHeader);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_ignores_pings_and_unchanged_reads()
    {
        var api = new Api(Status(needsApproval: true));
        var run = Start(api, json: false, TimeProvider.System);
        await WaitUntil(() => api.Streams == 1);

        await api.Raw(": ping\n\n");
        await api.Frame(2, "service.heartbeat");   // a read that changes nothing
        await WaitUntil(() => api.StatusReads == 2);
        await _interrupt.CancelAsync();

        Assert.Equal(0, await Finished(run));
        Assert.Equal(1, _out.ToString().Split("  profile", StringSplitOptions.None).Length - 1);   // printed once
        Assert.Equal(2, api.StatusReads);   // the ping caused no read of its own
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_1_when_the_process_stops()
    {
        var api = new Api(Status(needsApproval: true));
        var run = Start(api, json: false, TimeProvider.System);
        await WaitUntil(() => api.Streams == 1);

        api.Down = true;
        await api.End();

        Assert.Equal(1, await Finished(run));
        Assert.Contains("not running", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_falls_back_to_polling_on_429()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var api = new Api(Status(needsApproval: true), Status(needsApproval: false)) { StreamsFull = true };
        var run = Start(api, json: false, clock);
        await WaitUntil(() => api.Streams == 1);

        while (api.StatusReads < 2)
        {
            clock.Advance(FollowVerbs.Poll);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await _interrupt.CancelAsync();

        Assert.Equal(0, await Finished(run));
        Assert.Contains("(approved, hash", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_json_prints_one_body_per_line()
    {
        var first = Status(needsApproval: true);
        var second = Status(needsApproval: false);
        var api = new Api(first, second);
        var run = Start(api, json: true, TimeProvider.System);
        await WaitUntil(() => api.Streams == 1);

        await api.Frame(2, "service.heartbeat");
        await WaitUntil(() => api.StatusReads == 2);
        await WaitUntil(() => _out.ToString().Contains(second, StringComparison.Ordinal));
        await _interrupt.CancelAsync();

        Assert.Equal(0, await Finished(run));
        Assert.Equal([first, second], _out.ToString().ReplaceLineEndings("\n").TrimEnd().Split('\n'));
    }

    private static string Status(bool needsApproval) =>
        Encoding.UTF8.GetString(AwJson.Serialize(new GetStatusResponse(Running: true, 42, 3, LeaseExpiresAt: null,
            new ProfileStatus(needsApproval ? null : _hash, _hash, ChangedSinceApproval: false, Problem: null, "/p.yaml", needsApproval), []), SliceHarness.Json));

    private static Task<int> Finished(Task<int> run) => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "timed out waiting");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private Task<int> Start(Api api, bool json, TimeProvider clock)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), new InMemoryHumanTokenStore(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var verbs = new FollowVerbs(client, _probe, new Output(_out, _error, SliceHarness.Json), clock);
        return Task.Run(() => verbs.Run(json, _interrupt.Token));
    }

    /// <summary>The control plane: status bodies in turn (the last repeats), and an event stream the test writes to.</summary>
    private sealed class Api(params string[] bodies) : HttpMessageHandler
    {
        private readonly Pipe _stream = new();
        private int _reads;

        public int StatusReads => Volatile.Read(ref _reads);

        public int Streams { get; private set; }

        public string? StreamQuery { get; private set; }

        public string? LastAfterHeader { get; private set; }

        public bool Down { get; set; }

        public bool StreamsFull { get; init; }

        public async Task Frame(long id, string type) => await Raw(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"event: {type}\nid: {id}\ndata: {{}}\n\n"));

        public async Task Raw(string text) => _ = await _stream.Writer.WriteAsync(Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);

        public async Task End() => await _stream.Writer.CompleteAsync();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/sindri/events")
            {
                Streams++;
                StreamQuery = request.RequestUri.Query;
                return Task.FromResult(StreamsFull
                    ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("""{"title":"SND-API-012","status":429,"detail":"full"}""", Encoding.UTF8, "application/problem+json") }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(_stream.Reader.AsStream()) { Headers = { ContentType = new("text/event-stream") } } });
            }

            if (Down)
            {
                throw new HttpRequestException("connection refused");
            }

            LastAfterHeader = request.Headers.TryGetValues("X-Sindri-After", out var after) ? string.Join(',', after) : null;
            var body = bodies[Math.Min(Interlocked.Increment(ref _reads) - 1, bodies.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
