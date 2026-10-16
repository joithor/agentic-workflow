using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
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

    private readonly ListLogger<EventPump> _log = new();

    private Task Run(IService<StreamEventsRequest, StreamEventsResponse> read, CancellationToken stop, RecordingSignal? signal = null) =>
        new EventPump(_h.Store, signal ?? new RecordingSignal(_h.Clock), _h.Clock, SliceHarness.Json, _log).Run(new SseWriter(_body), read, new StreamEventsRequest(0), stop);

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_during_a_ledger_read_ends_the_stream_cleanly()
    {
        var read = new StoppedMidRead(_shutdown);

        await Run(read, _shutdown.Stopping);

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

    [Fact, Trait("Lane", "unit")]
    public async Task The_frames_match_the_framing_golden()
    {
        var writer = new SseWriter(_body);
        var ct = TestContext.Current.CancellationToken;

        await writer.Event(7, "service.heartbeat", "{\"slot\":\"2026-01-01T00:00:00Z\"}", ct);
        await writer.Comment("ping", ct);
        await writer.Event(id: null, "error", "{\"code\":\"SND-LEDGER-002\",\"detail\":\"ledger.db: database is locked\"}", ct);

        Golden.Match(_body.Text, typeof(EventStreamTests).Assembly, "Golden/Api/events/framing.sse", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_row_sent_with_a_null_payload_is_logged_once_with_its_sequence_and_type()
    {
        var unreadable = Event(7) with { Payload = JsonDocument.Parse("null").RootElement };
        var read = new ScriptedRead(new StreamEventsResponse([Event(6), unreadable], 7), new StreamEventsResponse([], 7));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var signal = new RecordingSignal(_h.Clock);
        var running = Run(read, stop.Token, signal);

        await signal.Entered(1);   // both rows are sent and the pump is waiting for more
        await stop.CancelAsync();
        await running;

        var line = Assert.Single(_log.Lines);
        Assert.Contains("7", line, StringComparison.Ordinal);
        Assert.Contains("service.heartbeat", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_client_that_has_gone_ends_the_stream_without_an_error()
    {
        _body.FailWrites = true;
        var read = new ScriptedRead(new StreamEventsResponse([Event(1)], 1), new StreamEventsResponse([], 1));

        await Run(read, TestContext.Current.CancellationToken);

        Assert.Equal(1, read.Reads);   // it didn't read on once the write failed
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_read_error_ends_with_an_error_frame()
    {
        var read = new ScriptedRead(new StreamEventsResponse([Event(1)], 1), SindriErrors.LedgerUnavailable.With("ledger.db: database is locked"));
        var signal = new RecordingSignal(_h.Clock);
        var running = Run(read, TestContext.Current.CancellationToken, signal);

        await signal.Entered(1);
        _h.Clock.Advance(EventPump.Poll);   // the wait ends, the next read fails
        await running;

        Assert.EndsWith("event: error\ndata: {\"code\":\"SND-LEDGER-002\",\"detail\":\"ledger.db: database is locked\"}\n\n", _body.Text, StringComparison.Ordinal);
        Assert.Equal(2, read.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ping_comment_after_exactly_15_seconds_without_a_frame()
    {
        var read = new ScriptedRead(new StreamEventsResponse([], 0));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var signal = new RecordingSignal(_h.Clock);
        var running = Run(read, stop.Token, signal);

        for (var second = 1; second < 15; second++)
        {
            await signal.Entered(second);
            _h.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        await signal.Entered(15);
        Assert.Equal(string.Empty, _body.Text);   // 14 seconds idle: no ping yet
        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        await signal.Entered(16);   // the loop wrote the ping on its way back to waiting
        await stop.CancelAsync();
        await running;

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
