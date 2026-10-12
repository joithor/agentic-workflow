using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.RebuildViews;

internal sealed class RebuildViewsService(IProjectorStatus status, IProjectorControl control, ILedgerStore ledger, HeldLease held, EventFactory events,
    ICallerContextAccessor callers, IIdGenerator ids) : IService<RebuildViewsRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(RebuildViewsRequest request, CancellationToken cancellationToken)
    {
        _ = (status, control, ledger, held, events, callers, ids);
        throw new NotSupportedException("RebuildViews: make RebuildViewsTests pass");
    }
}
