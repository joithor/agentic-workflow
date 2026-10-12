using Aw.Kernel;

namespace Aw.Sindri.Core.Ledger;

/// <summary>
/// The ledger's read-only port (API spec C9): the projector, queries and ledger reads use it. Its adapter opens the
/// ledger read-only, never migrates it and never creates the file: a missing ledger reads as empty with head 0.
/// </summary>
public interface ILedgerReader
{
    /// <summary>Events after <paramref name="sequence"/>, oldest first, at most <paramref name="max"/>.</summary>
    /// <param name="sequence">The cursor (0 for the start).</param>
    /// <param name="max">The most events to return, at least 1.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The events.</returns>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken);

    /// <summary>The most recent event of <paramref name="type"/>, or null.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The event, if any.</returns>
    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken);

    /// <summary>The ledger's head: the last recorded sequence, 0 when the ledger is empty or has no file yet.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The head.</returns>
    public ValueTask<Result<long>> Head(CancellationToken cancellationToken);
}
