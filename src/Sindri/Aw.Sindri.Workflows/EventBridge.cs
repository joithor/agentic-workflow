using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The ledger-to-Temporal bridge (engine spec rev 8 §5.4; engine P4 unit (d)): the built-in service 'workflow-events'
/// (S1-D8), on its own loop (S1-D10). Each run reads the ledger past its cursor and turns every registered event whose
/// envelope actor is its descriptor's producer into an 'event' signal: a 'route: item' event to every live instance of
/// its item, a 'route: causation' event only to the instance whose command caused it.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="catalog">The registered events.</param>
/// <param name="instances">The live instances, by a visibility query.</param>
/// <param name="origins">Which instance a command was written for.</param>
/// <param name="runtime">Signals an instance.</param>
/// <param name="cursor">The last delivered sequence.</param>
internal sealed class EventBridge(
    ILedgerStore ledger,
    BlockCatalog catalog,
    IWorkflowInstances instances,
    ICommandOrigins origins,
    IInstanceRuntime runtime,
    EventBridgeCursor cursor) : IScheduledService
{
    /// <summary>The service's name in the schedule ('workflow-events'; a service name is kebab-case).</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("workflow-events");

    /// <summary>The bridge's place in the schedule without a profile entry: every 5 s, the shortest cadence.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min));

    /// <summary>The actor of the triggers it delivers.</summary>
    public const string Actor = "service:workflow.events";

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (d): the bridge ({ledger.GetType().Name}, {catalog.GetType().Name}, {instances.GetType().Name}, {origins.GetType().Name}, {runtime.GetType().Name}, {cursor.GetType().Name})");
}
