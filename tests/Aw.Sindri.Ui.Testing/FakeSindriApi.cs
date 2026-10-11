using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The in-memory Sindri API, and the model the canned HTTP handler serves: a ping, a status, the ledger's events, and
/// open event streams that get each pushed event until they are dropped (the connection ends) or the server goes down.
/// </summary>
internal sealed class FakeSindriApi : ISindriApi
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

    /// <summary>How many status calls were made.</summary>
    public int StatusCalls { get; private set; }

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

    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken) =>
        Task.FromResult(Down ? Unreachable<PingView>(api) : Pong);

    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken)
    {
        StatusCalls++;
        return Task.FromResult(Down || FailStatus ? Unreachable<StatusView>(api) : Current);
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
