using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The event stream's own loop and writer, over a recording stream.</summary>
public sealed class EventStreamTests : IDisposable
{
    private readonly FakeShutdown _shutdown = new();
    private readonly SliceHarness _h = new();
    private readonly RecordingStream _body = new();

    public void Dispose()
    {
        _shutdown.Dispose();
        _body.Dispose();
    }

    private async Task Run(IService<StreamEventsRequest, StreamEventsResponse> read, TimeProvider clock, CancellationToken stop) =>
        await new EventPump(_h.Store, new LedgerSignal(clock), clock, SliceHarness.Json).Run(new SseWriter(_body), read, new StreamEventsRequest(0), stop);

    [Fact(Timeout = 5_000), Trait("Lane", "unit")]
    public async Task A_stop_during_a_ledger_read_ends_the_stream_cleanly()
    {
        var read = new StoppedMidRead(_shutdown);

        await Run(read, TimeProvider.System, _shutdown.Stopping);
        _ = TestContext.Current.CancellationToken;

        Assert.Equal((string.Empty, 1), (_body.Text, read.Reads));
    }

    [Fact(Timeout = 5_000), Trait("Lane", "unit")]
    public async Task Every_frame_is_flushed_as_soon_as_it_is_written()
    {
        var writer = new SseWriter(_body);

        await writer.Event(7, "service.heartbeat", "{\"a\":1}", TestContext.Current.CancellationToken);
        await writer.Comment("ping", TestContext.Current.CancellationToken);

        Assert.Equal(["write", "flush", "write", "flush"], _body.Operations);
        Assert.Equal("event: service.heartbeat\nid: 7\ndata: {\"a\":1}\n\n: ping\n\n", _body.Text);
    }

    [Fact(Timeout = 5_000), Trait("Lane", "unit")]
    public async Task A_client_that_has_gone_ends_the_stream_without_an_error()
    {
        _body.FailWrites = true;
        var read = new ScriptedRead(new StreamEventsResponse([Event(1)], 1), new StreamEventsResponse([], 1));

        await Run(read, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(1, read.Reads);   // it didn't read on once the write failed
    }

    [Fact(Timeout = 5_000), Trait("Lane", "unit")]
    public async Task A_read_error_ends_with_an_error_frame()
    {
        var read = new ScriptedRead(new StreamEventsResponse([Event(1)], 1), SindriErrors.LedgerUnavailable.With("ledger.db: database is locked"));

        await Run(read, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.EndsWith("event: error\ndata: {\"code\":\"SND-LEDGER-002\",\"detail\":\"ledger.db: database is locked\"}\n\n", _body.Text, StringComparison.Ordinal);
        Assert.Equal(2, read.Reads);
    }

    [Fact(Timeout = 10_000), Trait("Lane", "unit")]
    public async Task A_ping_comment_after_15_seconds_without_a_frame()
    {
        var ct = TestContext.Current.CancellationToken;
        var read = new ScriptedRead(new StreamEventsResponse([], 0));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = Run(read, _h.Clock, stop.Token);

        var seconds = 0;
        while (!_body.Text.Contains(": ping", StringComparison.Ordinal) && seconds < 20)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, ct);   // why: lets the loop reach its wait before the frozen clock moves
            _h.Clock.Advance(TimeSpan.FromSeconds(1));
            seconds++;
        }

        await stop.CancelAsync();
        await running;
        Assert.InRange(seconds, 15, 16);
        Assert.Equal(": ping\n\n", _body.Text);
    }

    private PublicEvent Event(long sequence) => new(sequence, EventId.From(new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), "service.heartbeat", 1, _h.Clock.GetUtcNow(), "service:host", "local", 0,
        Item: null, CorrelationId.From(new Guid(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), CausationId: null, TraceId: null, JsonDocument.Parse("{}").RootElement);

    /// <summary>A stream that records its writes and flushes in order, and can fail writes as a closed connection does.</summary>
    private sealed class RecordingStream : MemoryStream
    {
        public List<string> Operations { get; } = [];

        public bool FailWrites { get; set; }

        public string Text
        {
            get
            {
                lock (Operations)
                {
                    return Encoding.UTF8.GetString(ToArray());
                }
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
            {
                throw new IOException("the connection was closed");
            }

            lock (Operations)
            {
                Operations.Add("write");
                Write(buffer.Span);
            }

            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            lock (Operations)
            {
                Operations.Add("flush");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Answers each read with the next scripted page or error; the last one repeats.</summary>
    private sealed class ScriptedRead(params object[] script) : IService<StreamEventsRequest, StreamEventsResponse>
    {
        public int Reads { get; private set; }

        public ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
        {
            var next = script[Math.Min(Reads++, script.Length - 1)];
            Result<StreamEventsResponse> result = next is Error error ? error : (StreamEventsResponse)next;
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>A ledger read that a stop interrupts: the stop is requested while it runs, and it observes its token.</summary>
    private sealed class StoppedMidRead(FakeShutdown shutdown) : IService<StreamEventsRequest, StreamEventsResponse>
    {
        public int Reads { get; private set; }

        public ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
        {
            Reads++;
            shutdown.Request();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Result<StreamEventsResponse>>(new StreamEventsResponse([], request.After));
        }
    }
}
