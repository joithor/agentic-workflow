using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The PR babysitter's merge record (PB spec §4.7, BD7): an append-only log of what the instance did, 'babysit.jsonl' in its run area. Every block that owns a row kind
/// appends through this port, so a Step and an effect in different slices share one writer.
/// </summary>
public interface IMergeRecord
{
    /// <summary>Appends the row unless one with its identity is already in the record.</summary>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the row was written, false when its identity was already recorded; an error for an unknown kind or a run area that can't be read or written.</returns>
    public ValueTask<Result<bool>> Append(MergeRow row, CancellationToken cancellationToken);
}
