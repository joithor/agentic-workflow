using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The instance's suspension and pending park (spec §3.8, §5.1, §5.8, rev 8): what rev 7's two-deep frame stack did,
/// over two optional fields. A park, escalation or takeover becomes the suspension and leaves the recipe frame as it
/// was. A park that arrives while one is there waits as the pending park; a takeover over a park keeps that park as the
/// pending one, so the handback returns to it. Ending a suspension makes the pending park the suspension. Timers are the
/// interpreter's (§5.5): it stops, pauses and re-arms them around these changes.
/// </summary>
internal static class Suspensions
{
    private static OutcomeName Cancelled { get; } = OutcomeName.From("cancelled");

    /// <summary>
    /// Suspends the instance (spec §5.1, §5.8). A revoke's spec-conflict park replaces whatever is there, ending a
    /// takeover. A takeover covers a park or escalation, which becomes the pending park; a pending park already waiting
    /// is dropped then, which loses nothing: it can only be a drift park, and every later decision parks or refuses
    /// again while the drift lasts (§4.1). A drift park while already parked for drift is absorbed. Any other park that
    /// arrives while suspended waits as the pending park, unless one already waits.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="suspension">The park, escalation or takeover.</param>
    /// <returns>The suspended instance; the same instance when nothing changed.</returns>
    public static WorkflowInstance Suspend(WorkflowInstance instance, Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(suspension);
        if (instance.Ended)
        {
            return instance;
        }

        if (suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked })
        {
            return instance with { Suspension = suspension, PendingPark = null };
        }

        return suspension.Kind == SuspensionKind.Takeover ? Cover(instance, suspension) : Park(instance, suspension);
    }

    /// <summary>
    /// Ends the suspension (spec §5.8): the one there, or only when it mirrors <paramref name="child"/>. The recipe
    /// frame is as the suspension left it; a pending park becomes the suspension. Ending it counts no round.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="child">The child the suspension must mirror; null for one that mirrors none.</param>
    /// <returns>The instance after it, and what ended; SND-WF-003 when nothing fits.</returns>
    public static Result<Ended> End(WorkflowInstance instance, WorkflowInstanceId? child)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var address = instance.Address.Value;
        if (instance.Ended || instance.Suspension is not { } suspension)
        {
            return FitRefusals.Refuse($"{address} isn't suspended; there is nothing to resume", FitRefusals.Status(address));
        }

        if (suspension.Mirrors != child)
        {
            return FitRefusals.Refuse(
                child is { } id ? $"{address} doesn't mirror {id.Value} now; the child's change waits until it does" : $"{address} mirrors a child; it resumes when that child changes",
                FitRefusals.Status(address));
        }

        return new Ended(instance with { Suspension = instance.PendingPark, PendingPark = null }, suspension, PendingTookEffect: instance.PendingPark is not null);
    }

    /// <summary>A takeover (spec §3.8, §5.5): the human works the item; the interpreter pauses the state's timer and arms takeoverIdle.</summary>
    /// <param name="instance">The instance (it may be parked or escalated).</param>
    /// <param name="actor">Who takes over.</param>
    /// <returns>The taken-over instance, or SND-WF-003 when it already is or has ended.</returns>
    public static Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var address = instance.Address.Value;
        if (instance.Ended)
        {
            return FitRefusals.Refuse($"{address} has ended; there is nothing to take over", FitRefusals.Status(address));
        }

        if (instance.State == EngineStates.TakenOver)
        {
            return FitRefusals.Refuse($"{address} is already taken over", $"sindri workflow handback {address}");
        }

        return Suspend(instance, new Suspension(
            SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Question: string.Empty, actor, Mirrors: null,
            new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effect: null)));
    }

    /// <summary>A cancel (spec §3.8): the suspension and pending park are dropped, the instance ends in 'cancelled' with outcome 'cancelled'.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The cancelled instance.</returns>
    public static WorkflowInstance Cancel(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return instance.Ended
            ? instance
            : instance with
            {
                Frame = new Frame(EngineStates.Cancelled, instance.Frame.Entry, Run: null, Child: null),
                Suspension = null,
                PendingPark = null,
                Outcome = Cancelled,
            };
    }

    // A takeover covers what is there: the park or escalation waits as the pending park until the handback.
    private static WorkflowInstance Cover(WorkflowInstance instance, Suspension takeover) =>
        instance.Suspension?.Kind == SuspensionKind.Takeover
            ? instance
            : instance with { Suspension = takeover, PendingPark = instance.Suspension ?? instance.PendingPark };

    private static WorkflowInstance Park(WorkflowInstance instance, Suspension park)
    {
        if (park.ParkKind == ParkKind.Drift && ParkedForDrift(instance))
        {
            return instance;
        }

        if (instance.Suspension is null)
        {
            return instance with { Suspension = park };
        }

        return instance.PendingPark is null ? instance with { PendingPark = park } : instance;
    }

    private static bool ParkedForDrift(WorkflowInstance instance) =>
        instance.Suspension?.ParkKind == ParkKind.Drift || instance.PendingPark?.ParkKind == ParkKind.Drift;
}
