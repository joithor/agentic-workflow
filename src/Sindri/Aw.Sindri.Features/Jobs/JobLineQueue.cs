#pragma warning disable MA0182 // red stub: used once unit C is implemented
namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The bounded queue between a job's work and its progress writer (API P6, Q3): the work never blocks on the ledger, so a full queue drops its oldest
/// line and counts it; the reader sees '(N lines dropped)' before the next line it takes.
/// </summary>
internal sealed class JobLineQueue
{
    public JobLineQueue(int capacity)
    {
        _ = capacity;
    }

    /// <summary>Queues <paramref name="line"/>; at capacity the oldest queued line is dropped and counted.</summary>
    /// <param name="line">The line.</param>
    public void Offer(string line) => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Ends the queue: the reader gets what is queued and then stops.</summary>
    public void Complete() => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Every line in order, each run of drops announced by one '(N lines dropped)' line first, until the queue is completed and empty.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines.</returns>
    public IAsyncEnumerable<string> ReadAll(CancellationToken cancellationToken) => throw new NotSupportedException("red until API P6 unit C");
}
