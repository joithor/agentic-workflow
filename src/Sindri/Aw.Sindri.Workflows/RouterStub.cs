using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The router stub (workflow engine spec §12, engine P7 plan D8), a built-in service: for each 'workflow.item' instance that
/// started, it appends one 'item.routed' event as 'router.stub', whose outcome comes from the approved profile's
/// 'workflow.route' ('startable', or 'notStartable' for 'none') and whose 'fact:stepSpec' is the item's spec. The event
/// bridge delivers it to the instance. Real routing replaces it.
/// </summary>
/// <param name="ledger">The ledger it reads 'workflow.started' from and appends to.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="instances">Reads an instance's recipe and fact digests.</param>
/// <param name="store">Reads 'fact:item' and stores 'fact:stepSpec'.</param>
/// <param name="profile">The approved profile ('workflow.route').</param>
/// <param name="logger">The log an item it can't route is noted in.</param>
internal sealed class RouterStub(
    ILedgerStore ledger,
    HeldLease lease,
    EventFactory events,
    IInstanceReader instances,
    HomeFactStore store,
    IApprovedProfile profile,
    ILogger<RouterStub> logger) : IScheduledService
{
    /// <summary>The producer the 'item.routed' descriptor names.</summary>
    public const string Actor = "router.stub";

    /// <summary>The service's name in the schedule ('router-stub').</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("router-stub");

    /// <summary>The stub's place in the schedule without a profile entry: every 5 s.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min));

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <summary>Routes the 'workflow.item' instances that started since it last ran.</summary>
    /// <param name="run">The scheduled run.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Worked when it routed an item, else quiet.</returns>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("RouterStub.Run is engine P7 unit U3");
    }
}
