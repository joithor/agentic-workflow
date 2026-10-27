using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>
/// Reads the model picks from the ledger (model-pick spec §6, §6.1): the 'model.picking' and 'model.picked' rows of the item or
/// instance, each with its run's last 'session.usage' sample and how the run ended, oldest first.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
/// <param name="items">Resolves an item named by alias.</param>
/// <param name="repos">Resolves an item named by id, from the ledger.</param>
/// <param name="callers">The caller.</param>
internal sealed class GetLaunchPicksService(ILedgerReader ledger, EventFactory events, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers)
    : IService<GetLaunchPicksRequest, LaunchPicks>
{
    public ValueTask<Result<LaunchPicks>> Handle(GetLaunchPicksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(callers);
        throw new NotSupportedException("GetLaunchPicks is not built yet");
    }
}
