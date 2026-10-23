using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// Reads what <see cref="BabysitterStatsCounter"/> counts: the workflow ledger's rows and the merge record of each babysitter instance started in the window. Nothing is cached: the merge records are
/// files the effects append to, so no ledger head says when they changed.
/// </summary>
/// <param name="rows">The workflow ledger rows.</param>
/// <param name="records">Reads a merge record.</param>
/// <param name="clock">The clock.</param>
public sealed class BabysitterStatsSource(IWorkflowStatRows rows, IMergeRecordReader records, TimeProvider clock)
{
    /// <summary>Counts the babysitter's figures for the window.</summary>
    /// <param name="window">The stats window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The figures, or the error of the ledger read. A merge record that can't be read counts as one record error.</returns>
    public async ValueTask<Result<BabysitterStats>> Of(StatsWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        var read = await rows.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var now = clock.GetUtcNow();
        var found = new Dictionary<string, MergeRecordRows>(StringComparer.Ordinal);
        foreach (var address in BabysitterStatsCounter.Instances(read.Value, window, now))
        {
            var record = await records.Read(address, cancellationToken).ConfigureAwait(false);
            found[address] = record.IsError ? new MergeRecordRows([], 1) : record.Value;
        }

        return BabysitterStatsCounter.Count(read.Value, found, window, now);
    }
}
