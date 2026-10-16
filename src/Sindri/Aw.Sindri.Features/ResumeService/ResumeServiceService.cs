using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ResumeService;

/// <summary>Resumes a service (API P6 unit D): SND-SVC-404 for an unknown one, then one 'service.resumed'. Red until then.</summary>
internal sealed class ResumeServiceService : IService<ResumeServiceRequest, CommandAccepted>
{
    public ResumeServiceService(IApprovedProfile approvals, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    {
        _ = (approvals, ledger, held, events, callers);
    }

    public ValueTask<Result<CommandAccepted>> Handle(ResumeServiceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit D");
}
