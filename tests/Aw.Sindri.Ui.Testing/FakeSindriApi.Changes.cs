using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The instance-change stream of the fake: opened streams get each pushed change until they are dropped.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<Channel<InstanceChange>> _changeStreams = [];
    private readonly List<string?> _changeQueries = [];

    /// <summary>What happened, in order, for a test that cares about it: "stream-open" when an instance-change stream opened. Tests add their own lines.</summary>
    public ConcurrentQueue<string> Trace { get; } = new();

    /// <summary>The instance each instance-change stream was opened for (null: every instance), in order.</summary>
    public IReadOnlyList<string?> ChangeQueries { get { lock (_gate) { return [.. _changeQueries]; } } }

    /// <summary>Sends a frame to every open instance-change stream (one for another instance than the stream's is not sent to it).</summary>
    /// <param name="changes">The frames.</param>
    public void PushChange(params InstanceChange[] changes)
    {
        lock (_gate)
        {
            for (var i = 0; i < _changeStreams.Count; i++)
            {
                foreach (var change in changes.Where(c => _changeQueries[i] is null || string.Equals(_changeQueries[i], c.Instance, StringComparison.Ordinal)))
                {
                    _ = _changeStreams[i].Writer.TryWrite(change);
                }
            }
        }
    }

    public async IAsyncEnumerable<InstanceStreamItem> InstanceChanges(Uri api, string? instance, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Down || StreamsEndAtOnce || Refusal is not null)
        {
            yield break;
        }

        var stream = Channel.CreateUnbounded<InstanceChange>();
        lock (_gate)
        {
            _changeStreams.Add(stream);
            _changeQueries.Add(instance);
        }

        Trace.Enqueue("stream-open");
        yield return new InstanceStreamItem(Change: null);
        await foreach (var change in stream.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new InstanceStreamItem(change);
        }
    }

    private void DropChanges()
    {
        lock (_gate)
        {
            foreach (var s in _changeStreams)
            {
                _ = s.Writer.TryComplete();
            }

            _changeStreams.Clear();
            _changeQueries.Clear();
        }
    }
}
