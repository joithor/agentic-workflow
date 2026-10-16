using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds 'service.ran' into the 'services' view (API spec §2.3): one row per service, keyed by its name, holding its last run.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ServicesProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Services];

    public bool Handles(string eventType) => string.Equals(eventType, EventTypes.ServiceRan, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var ran = events.Payload<ServiceRan>(recorded);
        if (ran.IsError)
        {
            return ran.Error;   // an unreadable payload stalls the projector here (SND-VIEW-001)
        }

        var key = ran.Value.Service.Value;
        var row = await rows.Get<ServiceRow>(ViewNames.Services, key, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Services, key, Fold(row.Value, ran.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row after <paramref name="ran"/>: the run it records becomes the last run.</summary>
    /// <param name="row">The row so far, or null before the service's first run.</param>
    /// <param name="ran">The payload.</param>
    /// <param name="recorded">The recorded event (its time and sequence).</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServiceRan ran, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(ran);
        ArgumentNullException.ThrowIfNull(recorded);
        var last = new LastRun(ran.Slot, ran.Outcome, ran.Code, recorded.Envelope.OccurredAt);
        return new ServiceRow(ran.Service, last, row?.Paused ?? false, recorded.Sequence);
    }

    /// <summary>The row after a 'service.paused': paused, the last run and scheduled slot kept (a row is made when there is none).</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="paused">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServicePaused paused, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");

    /// <summary>The row after a 'service.resumed': not paused, the rest kept.</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="resumed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServiceResumed resumed, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");
}
