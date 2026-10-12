using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The in-memory Sindri API, and the model the canned HTTP handler serves: a ping, a status, the ledger's events, and
/// open event streams that get each pushed event until they are dropped (the connection ends) or the server goes down.
/// The catalog, the services view and the rebuild command answer with golden bodies (<see cref="RouteAnswer"/>), the
/// same text the handler serves.
/// </summary>
internal sealed partial class FakeSindriApi : ISindriApi
{
    private readonly Lock _gate = new();
    private readonly List<EventView> _events = [];
    private readonly List<Channel<EventView>> _streams = [];

    /// <summary>The ping answer.</summary>
    public PingView Pong { get; set; } = new(4242, 7);

    /// <summary>The status answer.</summary>
    public StatusView Current { get; set; } = A.Status();

    /// <summary>True when nothing answers.</summary>
    public bool Down { get; private set; }

    /// <summary>When true, every event stream ends as soon as it opens (an events route that answers and closes).</summary>
    public bool StreamsEndAtOnce { get; set; }

    /// <summary>When true, status calls fail as unreachable while pings and open streams carry on (a stopping process).</summary>
    public bool FailStatus { get; set; }

    /// <summary>When true, status calls answer SND-UI-003 (an answer this build can't read) while pings carry on.</summary>
    public bool UnreadableStatus { get; set; }

    /// <summary>What an unreadable status says (SND-UI-003's message), for a layout test that needs a long one.</summary>
    public string UnreadableReason { get; set; } = "/sindri/status answered 500";

    /// <summary>When set, each status call waits for it before it answers (a slow control plane); null answers at once.</summary>
    public TaskCompletionSource? StatusGate { get; set; }

    /// <summary>When true, pings fail as unreachable while status calls and open streams carry on.</summary>
    public bool FailPing { get; set; }

    /// <summary>How many status calls were made.</summary>
    public int StatusCalls { get; private set; }

    /// <summary>How many pings were made.</summary>
    public int PingCalls => Volatile.Read(ref _pingCalls);

    private int _pingCalls;

    /// <summary>The cursor each event stream was opened with.</summary>
    public IReadOnlyList<long> StreamCursors => [.. _cursors];

    private readonly List<long> _cursors = [];

    /// <summary>Appends an event to the ledger and sends it to every open stream.</summary>
    /// <param name="events">The events.</param>
    public void Push(params EventView[] events)
    {
        lock (_gate)
        {
            foreach (var e in events)
            {
                _events.Add(e);
                foreach (var s in _streams)
                {
                    _ = s.Writer.TryWrite(e);
                }
            }
        }
    }

    /// <summary>Ends every open stream, as a dropped connection does.</summary>
    public void Drop()
    {
        lock (_gate)
        {
            foreach (var s in _streams)
            {
                _ = s.Writer.TryComplete();
            }

            _streams.Clear();
        }
    }

    /// <summary>Takes the server down: calls fail and open streams end.</summary>
    public void GoDown()
    {
        Down = true;
        Drop();
    }

    /// <summary>Brings it back.</summary>
    public void ComeUp() => Down = false;

    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _pingCalls);
        return Task.FromResult(Down || FailPing ? Unreachable<PingView>(api) : Pong);
    }

    public async Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken)
    {
        StatusCalls++;
        if (StatusGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Down || FailStatus ? Unreachable<StatusView>(api)
            : UnreadableStatus ? SindriUiErrors.BadAnswer.With(UnreadableReason)
            : Current;
    }

    public async IAsyncEnumerable<EventView> Events(Uri api, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Down || StreamsEndAtOnce)
        {
            lock (_gate)
            {
                _cursors.Add(after);
            }

            yield break;
        }

        var stream = Channel.CreateUnbounded<EventView>();
        lock (_gate)
        {
            _cursors.Add(after);
            foreach (var e in _events.Where(e => e.Sequence > after))
            {
                _ = stream.Writer.TryWrite(e);
            }

            _streams.Add(stream);
        }

        await foreach (var e in stream.Reader.ReadAllAsync(cancellationToken))
        {
            yield return e;
        }
    }

    private static Result<T> Unreachable<T>(Uri api) => SindriUiErrors.Unreachable.With($"nothing answers at {api.Authority}");
}
