using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StopProcess;

/// <summary>
/// Records the stop, then lets go in the order a client can trust: forget the grant (so ping refuses), request the
/// shutdown, remove control.json, and only then release the lease. A client that still finds control.json never finds
/// this process answering without the lease.
/// </summary>
internal sealed class StopProcessService(HeldLease held, ILease lease, ILedgerStore ledger, EventFactory events, IShutdown shutdown, IEndpointFile endpoints)
    : IService<StopProcessRequest, StopProcessResponse>
{
    public async ValueTask<Result<StopProcessResponse>> Handle(StopProcessRequest request, CancellationToken cancellationToken)
    {
        var grant = held.Current;
        if (grant is null)
        {
            shutdown.Request();
            return new StopProcessResponse(0, 0);
        }

        var entry = events.Entry(EventTypes.ProcessStopped, new ProcessStopped(grant.Holder.Pid, grant.Epoch, request.Reason),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProcessStopped}:{grant.Epoch}"));
        var recorded = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        held.Clear();         // first, so a renewal finishing now can't hold the grant again
        shutdown.Request();   // before the release, so a renewal that runs after it sees a stop, not a lost lease

        // Past the point of no return: the process is stopping whatever the caller does, so a client that hangs up now
        // must not cancel the removal or the release (the lease would stay held until it expires).
        var removed = await endpoints.Remove(grant.Holder.Pid, CancellationToken.None).ConfigureAwait(false);   // before the release
        var released = await lease.Release(grant, CancellationToken.None).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error
            : removed.IsError ? removed.Error
            : released.IsError ? released.Error
            : new StopProcessResponse(grant.Holder.Pid, grant.Epoch);
    }
}
