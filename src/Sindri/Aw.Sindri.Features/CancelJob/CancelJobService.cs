using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CancelJob;

/// <summary>Cancels a job (API P6 unit E): JobRules.CanCancel, then 'job.cancelled' and the token. Red until then.</summary>
internal sealed class CancelJobService : IService<CancelJobRequest, CommandAccepted>
{
    public CancelJobService(IJobViews jobs, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, IJobCancellation runner)
    {
        _ = (jobs, ledger, held, events, callers, runner);
    }

    public ValueTask<Result<CommandAccepted>> Handle(CancelJobRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit E");
}
