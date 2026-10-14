using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListOutbox;

/// <summary>
/// Folds the ledger's effect rows ('outbox.pending', 'outbox.dispatched', 'outbox.refused') by command, the latest row
/// winning, and keeps those in the requested state (engine spec §6.3; engine P6 unit (c)).
/// </summary>
internal sealed class ListOutboxService : IService<ListOutboxRequest, ListOutboxResponse>
{
    public ValueTask<Result<ListOutboxResponse>> Handle(ListOutboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new NotSupportedException("engine P6 unit (c): ListOutbox (outbox list over the ledger's effect rows)");
    }
}
