using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StopProcess;

/// <summary>
/// Records the stop, requests the shutdown, stops the Temporal dev server, then releases the lease: the server goes down
/// before the lease is free, so the next serve never adopts a server that is stopping. Without a lease this process owns
/// no server, so it only shuts down.
/// </summary>
internal sealed class StopProcessService(HeldLease held, ILease lease, ILedgerStore ledger, EventFactory events, IShutdown shutdown, ITemporalServer temporal)
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
        _ = temporal;   // red: engine P0 unit (a) stops the dev server here
        var stopped = Results.Success(Unit.Value);
        var released = await lease.Release(grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error
            : stopped.IsError ? stopped.Error
            : released.IsError ? released.Error
            : new StopProcessResponse(grant.Holder.Pid, grant.Epoch);
    }
}
