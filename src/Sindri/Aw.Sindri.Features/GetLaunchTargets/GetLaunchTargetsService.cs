using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLaunchTargets;

/// <summary>Serves the newest 'launch.probed' with each target's daily budget and today's spend (UI spec E-3, screens spec E-28).</summary>
/// <param name="ledger">The ledger, for the newest probe.</param>
/// <param name="events">Reads the payload.</param>
/// <param name="approvals">The approved profile: budgets.</param>
/// <param name="spend">What each agent has spent today.</param>
/// <param name="status">How fresh the views are.</param>
internal sealed class GetLaunchTargetsService(ILedgerStore ledger, EventFactory events, IApprovedProfile approvals, ILaunchSpend spend, IProjectorStatus status) : IService<GetLaunchTargetsRequest, LaunchTargets>
{
    public ValueTask<Result<LaunchTargets>> Handle(GetLaunchTargetsRequest request, CancellationToken cancellationToken)
    {
        _ = (ledger, events, approvals, spend, status);
        throw new NotSupportedException("GetLaunchTargets: make GetLaunchTargetsTests pass");
    }
}
