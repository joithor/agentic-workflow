using Aw.Kernel;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// How far the ledger side of the engine is behind (API spec §3.8, E-30): the ledger rows past the event bridge's cursor, which it has not yet delivered into Temporal, and how long the
/// oldest of them has waited. The mirror the other way, Temporal's events into the ledger, is the RecordEvent activity, which the workflow waits for, so it never lags.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="cursor">The bridge's cursor.</param>
/// <param name="clock">The clock the wait is measured on.</param>
internal sealed class EngineMirrorLag(ILedgerReader ledger, EventBridgeCursor cursor, TimeProvider clock)
{
    /// <summary>The lag now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lag, or why the ledger or the cursor can't be read.</returns>
    public async ValueTask<Result<EngineMirror>> Read(CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var delivered = await cursor.Read(cancellationToken).ConfigureAwait(false);
        if (delivered.IsError)
        {
            return delivered.Error;
        }

        if (head.Value <= delivered.Value)
        {
            return new EngineMirror(0, 0);
        }

        var next = await ledger.ReadAfter(delivered.Value, 1, cancellationToken).ConfigureAwait(false);
        if (next.IsError)
        {
            return next.Error;
        }

        var waited = next.Value.Count == 0 ? TimeSpan.Zero : clock.GetUtcNow() - next.Value[0].Envelope.OccurredAt;
        return new EngineMirror(head.Value - delivered.Value, Math.Max(0, (long)waited.TotalMilliseconds));
    }
}
