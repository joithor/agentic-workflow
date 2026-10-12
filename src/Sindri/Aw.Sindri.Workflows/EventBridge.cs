using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The ledger-to-Temporal bridge (engine spec rev 8 §5.4; engine P4 unit (d)): the built-in service 'workflow-events'
/// (S1-D8), on its own loop (S1-D10). Each run reads the ledger past its cursor, in order, and turns every registered
/// event whose envelope actor is its descriptor's producer into an 'event' signal: a 'route: item' event to every live
/// instance of its item (Temporal buffers it until the instance reaches the wait, so an early event isn't lost), a
/// 'route: causation' event only to the instance whose command caused it. Any other event, or one from another actor,
/// signals nothing. The cursor moves past an event once every target has it; an instance that has ended meanwhile is
/// skipped, and a Temporal or ledger failure ends the run with the cursor before that event, so the next run retries it.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="catalog">The registered events.</param>
/// <param name="instances">The live instances, by a visibility query on 'Item'.</param>
/// <param name="origins">Which instance a command was written for.</param>
/// <param name="runtime">Signals an instance.</param>
/// <param name="cursor">The last delivered sequence.</param>
/// <param name="logger">The log a refused or unroutable event is noted in.</param>
internal sealed partial class EventBridge(
    ILedgerStore ledger,
    BlockCatalog catalog,
    IWorkflowInstances instances,
    ICommandOrigins origins,
    IInstanceRuntime runtime,
    EventBridgeCursor cursor,
    ILogger<EventBridge> logger) : IScheduledService
{
    /// <summary>The actor of the triggers it delivers.</summary>
    public const string Actor = "service:workflow.events";

    private const int Batch = 100;

    /// <summary>The service's name in the schedule ('workflow-events'; a service name is kebab-case).</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("workflow-events");

    /// <summary>The bridge's place in the schedule without a profile entry: every 5 s, the shortest cadence.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min));

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var from = await cursor.Read(cancellationToken).ConfigureAwait(false);
        if (from.IsError)
        {
            return from.Error;
        }

        for (var at = from.Value; ;)
        {
            var read = await ledger.ReadAfter(at, Batch, cancellationToken).ConfigureAwait(false);
            if (read.IsError || read.Value.Count == 0)
            {
                return read.IsError ? read.Error : Unit.Value;
            }

            foreach (var recorded in read.Value)
            {
                var delivered = await Deliver(recorded, cancellationToken).ConfigureAwait(false);
                var moved = delivered.IsError ? delivered : await cursor.Advance(recorded.Sequence, cancellationToken).ConfigureAwait(false);
                if (moved.IsError)
                {
                    return moved.Error;
                }

                at = recorded.Sequence;
            }
        }
    }

    /// <summary>Signals <paramref name="recorded"/> to every instance it routes to; nothing when it isn't a registered event from its producer.</summary>
    private async ValueTask<Result<Unit>> Deliver(LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var descriptor = catalog.Events.FirstOrDefault(d => string.Equals(d.Kind.Value, recorded.Envelope.Type, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return Unit.Value;
        }

        if (!string.Equals(recorded.Envelope.Actor, descriptor.Producer, StringComparison.Ordinal))
        {
            NotFromProducer(logger, recorded.Sequence, recorded.Envelope.Type, recorded.Envelope.Actor, descriptor.Producer);
            return Unit.Value;
        }

        var targets = await Targets(descriptor, recorded, cancellationToken).ConfigureAwait(false);
        if (targets.IsError)
        {
            return targets.Error;
        }

        var (trigger, problem) = DeliveredEvents.Trigger(descriptor, recorded, Actor);
        if (trigger is null)
        {
            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, problem ?? "it makes no trigger");
            return Unit.Value;
        }

        foreach (var target in targets.Value)
        {
            var signalled = await runtime.Signal(target, trigger, cancellationToken).ConfigureAwait(false);
            if (signalled.IsError && signalled.Error.Code != WorkflowErrors.InstanceNotFound.Code)
            {
                return signalled.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>The instances <paramref name="recorded"/> routes to (spec §5.4): its item's live instances, or its command's instance.</summary>
    private async ValueTask<Result<IReadOnlyList<InstanceAddress>>> Targets(EventDescriptor descriptor, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        if (descriptor.Route == EventRoute.Causation)
        {
            var causer = await origins.CauserOf(recorded, cancellationToken).ConfigureAwait(false);
            if (causer.IsError)
            {
                return causer.Error;
            }

            if (causer.Value is { } address)
            {
                return new[] { address };
            }

            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, "its causation names no instance's command");
            return Array.Empty<InstanceAddress>();
        }

        if (recorded.Item is not { Length: > 0 } item)
        {
            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, "it names no item");
            return Array.Empty<InstanceAddress>();
        }

        var live = await instances.List($"Item = '{item.Replace("'", "''", StringComparison.Ordinal)}'", temporal: null, cancellationToken).ConfigureAwait(false);
        return live.IsError
            ? live.Error
            : live.Value.Select(static row => InstanceAddress.TryFrom(row.Address, out var address) ? address : (InstanceAddress?)null).OfType<InstanceAddress>().ToList();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "event {Sequence} ({Type}) was written by {Actor}, not its producer {Producer}; nothing was signalled")]
    private static partial void NotFromProducer(ILogger logger, long sequence, string type, string actor, string producer);

    [LoggerMessage(Level = LogLevel.Information, Message = "event {Sequence} ({Type}) routes to no instance: {Why}")]
    private static partial void NoRoute(ILogger logger, long sequence, string type, string why);
}
