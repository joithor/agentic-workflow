using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The bounded queue between a job's work and its progress writer (API P6, Q3): the work never blocks on the ledger, so a full queue drops its oldest
/// line and counts it; the reader sees '(N lines dropped)' before the next line it takes.
/// </summary>
internal sealed class JobLineQueue
{
    private readonly Channel<string> _lines;
    private long _dropped;

    /// <summary>Makes a queue holding at most <paramref name="capacity"/> lines.</summary>
    /// <param name="capacity">The most lines waiting.</param>
    public JobLineQueue(int capacity)
    {
        _lines = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>Queues <paramref name="line"/>; at capacity the oldest queued line is dropped and counted.</summary>
    /// <param name="line">The line.</param>
    public void Offer(string line) => _ = _lines.Writer.TryWrite(line);

    /// <summary>Ends the queue: the reader gets what is queued and then stops.</summary>
    public void Complete() => _ = _lines.Writer.TryComplete();

    /// <summary>Every line in order, each run of drops announced by one '(N lines dropped)' line first, until the queue is completed and empty.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines.</returns>
    public async IAsyncEnumerable<string> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in _lines.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var dropped = Interlocked.Exchange(ref _dropped, 0);
            if (dropped > 0)
            {
                yield return string.Create(CultureInfo.InvariantCulture, $"({dropped} {(dropped == 1 ? "line" : "lines")} dropped)");
            }

            yield return line;
        }
    }
}
