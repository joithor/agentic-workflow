using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>Red: engine P4 unit (e) builds it.</summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
internal sealed class GetInstanceEventsService(ILedgerReader ledger, EventFactory events) : IService<GetInstanceEventsRequest, GetInstanceEventsResponse>
{
    public ValueTask<Result<GetInstanceEventsResponse>> Handle(GetInstanceEventsRequest request, CancellationToken cancellationToken) =>
        ledger is null || events is null
            ? throw new InvalidOperationException("no ledger")
            : throw new NotSupportedException("GetInstanceEventsService: engine P4 unit (e) makes its tests pass");
}
