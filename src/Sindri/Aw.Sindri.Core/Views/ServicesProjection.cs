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
        => throw new NotSupportedException("ServicesProjection.Fold: P2 unit C");
}
