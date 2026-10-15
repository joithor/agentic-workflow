using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>
/// A small projection for <see cref="ViewStoreContract"/>: an event of <paramref name="type"/> writes a
/// <see cref="ServiceRow"/> named by the event's idempotency key into <paramref name="view"/>; with
/// <paramref name="fails"/> it writes a 'ghost' row and then fails, so a savepoint must drop the write.
/// </summary>
/// <param name="view">The view it writes.</param>
/// <param name="type">The event type it handles.</param>
/// <param name="fails">True to fail every event it handles.</param>
public sealed class FixtureProjection(string view, string type, bool fails = false) : IProjection
{
    /// <summary>The error a failing fixture returns.</summary>
    public static Error Failure { get; } = SindriErrors.InvalidInput.With("fixture: this event can't be folded");

    /// <inheritdoc/>
    public IReadOnlyList<string> Views => [view];

    /// <inheritdoc/>
    public bool Handles(string eventType) => string.Equals(eventType, type, StringComparison.Ordinal);

    /// <summary>The row an event folds into: its key names the service, and the last run comes from the envelope.</summary>
    /// <param name="recorded">The event.</param>
    /// <returns>The row.</returns>
    public static ServiceRow RowFor(LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        var at = recorded.Envelope.OccurredAt;
        return new ServiceRow(ServiceName.From(recorded.IdempotencyKey),
            new LastRun(at, ServiceOutcome.Failed, "SND-SCHED-001", at.AddSeconds(1)), Paused: false, recorded.Sequence);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (fails)
        {
            _ = await rows.Put(view, "ghost", RowFor(recorded) with { Name = ServiceName.From("ghost") }, cancellationToken).ConfigureAwait(false);
            return Failure;
        }

        return await rows.Put(view, recorded.IdempotencyKey, RowFor(recorded), cancellationToken).ConfigureAwait(false);
    }
}
