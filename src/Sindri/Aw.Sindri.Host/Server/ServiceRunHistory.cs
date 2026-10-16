using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The latest 'service.ran' of every service, from one pass over the ledger's service.ran events (a type-filtered scan,
/// so the pass skips the other rows). The scheduler anchors its schedule on the slots and decides an idle run's recording
/// from the outcomes.
/// </summary>
internal static class ServiceRunHistory
{
    private const int Page = 500;

    private static readonly LedgerFilter _ran = new(TypeFilter.Parse(EventTypes.ServiceRan).Value, Item: null);

    /// <summary>Reads the latest run of every service that has one; an unreadable ledger is its error.</summary>
    /// <param name="reader">The ledger.</param>
    /// <param name="events">Decodes the payloads.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each service's latest run.</returns>
    public static async Task<Result<IReadOnlyDictionary<ServiceName, LastRun>>> Read(ILedgerReader reader, EventFactory events, CancellationToken cancellationToken)
    {
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var latest = new Dictionary<ServiceName, LastRun>();
        for (var after = 0L; after < head.Value;)
        {
            var scan = await reader.Scan(after, Page, _ran, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var row in scan.Value.Events)
            {
                var ran = events.Payload<ServiceRan>(row);
                if (ran.IsSuccess)
                {
                    latest[ran.Value.Service] = new LastRun(ran.Value.Slot, ran.Value.Outcome);
                }
            }

            if (scan.Value.ScannedTo <= after)
            {
                break;   // a reader that makes no progress must not loop here
            }

            after = scan.Value.ScannedTo;
        }

        return latest;
    }
}
