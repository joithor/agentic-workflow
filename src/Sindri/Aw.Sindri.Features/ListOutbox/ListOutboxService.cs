using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ListOutbox;

/// <summary>
/// Folds the ledger's effect rows ('outbox.pending', 'outbox.dispatched', 'outbox.refused') by command, the latest row
/// winning, and keeps those in the requested state (engine spec §6.3; engine P6 unit (c)).
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each row's payload.</param>
internal sealed class ListOutboxService(ILedgerReader ledger, EventFactory events) : IService<ListOutboxRequest, ListOutboxResponse>
{
    public ValueTask<Result<ListOutboxResponse>> Handle(ListOutboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new NotSupportedException($"engine P6 unit (c): ListOutbox (outbox list over the ledger's effect rows; {ledger.GetType().Name}, {events.GetType().Name})");
    }
}
