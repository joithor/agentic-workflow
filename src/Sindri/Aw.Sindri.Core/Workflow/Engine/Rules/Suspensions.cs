using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The instance's suspension and its queue of pending parks (spec §3.8, §5.1, §5.8, rev 8.1). A park, escalation or
/// takeover becomes the suspension and leaves the recipe frame as it was. A park that arrives while one is there waits in
/// the queue (<see cref="WorkflowInstance.PendingPark"/>, then <see cref="WorkflowInstance.LaterParks"/>); a takeover
/// over a park puts that park first in the queue, so the handback returns to it. Ending a suspension makes the first
/// queued park the suspension. A refused effect is held by exactly one drift or dispatch park, wherever it sits. Nothing
/// is ever dropped. Timers are the interpreter's (§5.5): it stops, pauses and re-arms them around these changes.
/// </summary>
internal static class Suspensions
{
    /// <summary>The parks waiting behind the suspension, in order: the pending park, then the queued ones.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The pending parks.</returns>
    internal static IReadOnlyList<Suspension> PendingParks(WorkflowInstance instance) =>
        instance.PendingPark is { } pending ? [pending, .. instance.LaterParks ?? []] : [];

    /// <summary>The instance with <paramref name="parks"/> as its pending queue.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="parks">The queue, first to take effect first.</param>
    /// <returns>The instance.</returns>
    internal static WorkflowInstance WithPending(WorkflowInstance instance, IReadOnlyList<Suspension> parks) => instance with
    {
        PendingPark = parks.Count > 0 ? parks[0] : null,
        LaterParks = parks.Count > 1 ? [.. parks.Skip(1)] : null,
    };

    /// <summary>
    /// Takes the first park that can become the suspension off the queue; drift parks the re-approval already ended
    /// stay queued (they only hold effects to re-send).
    /// </summary>
    /// <param name="instance">The instance whose suspension is ending.</param>
    /// <returns>The instance with the queue after it, the park to make the suspension (null when none), and the effects of approved parks to re-send (null while a park takes effect).</returns>
    internal static (WorkflowInstance Instance, Suspension? Next, IReadOnlyList<RefusedEffect>? Released) Advance(WorkflowInstance instance)
    {
        var queue = PendingParks(instance).ToList();
        var index = queue.FindIndex(static p => !p.Approved);
        if (index >= 0)
        {
            var next = queue[index];
            queue.RemoveAt(index);
            return (WithPending(instance, queue) with { Suspension = next }, next, null);
        }

        var released = queue.SelectMany(static p => p.Resume.Effects).DistinctBy(static e => e.Identity).ToList();
        return (WithPending(instance, []) with { Suspension = null }, null, released.Count > 0 ? released : null);
    }

    private static OutcomeName Cancelled { get; } = OutcomeName.From("cancelled");

    /// <summary>
    /// Suspends the instance (spec §5.1, §5.8). A revoke's spec-conflict park replaces whatever is there, ending a
    /// takeover. A takeover covers a park or escalation, which goes first in the queue behind it. A park carrying effects
    /// joins the first resendable drift or dispatch park; a drift park while already parked for drift is absorbed; any
    /// other park that arrives while suspended joins the queue (an equal one already there absorbs it).
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
            return WithPending(instance, []) with { Suspension = suspension };
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

        var (after, next, released) = Advance(instance);
        return new Ended(after, suspension, PendingTookEffect: next is not null, released);
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
            new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effects: [])));
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
                LaterParks = null,
                Outcome = Cancelled,
            };
    }

    // A takeover covers what is there: the park or escalation joins the front of the pending queue until the handback; nothing waiting is dropped.
    private static WorkflowInstance Cover(WorkflowInstance instance, Suspension takeover)
    {
        if (instance.Suspension?.Kind == SuspensionKind.Takeover)
        {
            return instance;
        }

        var queue = PendingParks(instance);
        return WithPending(instance, instance.Suspension is { } covered ? [covered, .. queue] : queue) with { Suspension = takeover };
    }

    private static WorkflowInstance Park(WorkflowInstance instance, Suspension park)
    {
        if (park.Resume.Effects.Count > 0 && Merged(instance, park) is { } merged)
        {
            return merged;
        }

        if (park.ParkKind == ParkKind.Drift && ParkedForDrift(instance))
        {
            return instance;   // absorbed: a drift park already holds this instance
        }

        if (instance.Suspension is null)
        {
            return instance with { Suspension = park };
        }

        var queue = PendingParks(instance);
        return queue.Any(held => SameKind(held, park)) || SameKind(instance.Suspension, park)
            ? instance
            : WithPending(instance, [.. queue, park]);
    }

    private static bool SameKind(Suspension a, Suspension b) =>
        a is { Kind: SuspensionKind.Park } && !a.Approved && a.Reason == b.Reason && a.ParkKind == b.ParkKind && a.Mirrors == b.Mirrors
        && string.Equals(a.Question, b.Question, StringComparison.Ordinal) && a.Resume.Effects.Count == 0 && b.Resume.Effects.Count == 0;

    /// <summary>
    /// A park that carries effects joins the first drift or dispatch park whose resume re-sends effects, wherever it
    /// sits (the suspension or the queue), de-duplicated by identity: a refused effect is held by exactly one park.
    /// Null when no such park exists, so the park waits (or becomes the suspension) itself.
    /// </summary>
    private static WorkflowInstance? Merged(WorkflowInstance instance, Suspension park)
    {
        var queue = PendingParks(instance);
        var all = (instance.Suspension is { } current ? new[] { current } : []).Concat(queue).ToList();
        var at = all.FindIndex(Resendable);
        if (at < 0)
        {
            return null;
        }

        var held = all[at];
        var known = held.Resume.Effects.Select(static e => e.Identity).ToHashSet();
        var added = park.Resume.Effects.Where(e => !known.Contains(e.Identity)).ToList();
        if (added.Count == 0)
        {
            return instance;
        }

        var grown = held with { Resume = held.Resume with { Effects = [.. held.Resume.Effects, .. added] } };
        return instance.Suspension is not null && at == 0
            ? instance with { Suspension = grown }
            : WithPending(instance, [.. queue.Select((p, i) => i == at - (instance.Suspension is null ? 0 : 1) ? grown : p)]);
    }

    private static bool Resendable(Suspension held) =>
        held is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift or ParkKind.Dispatch, Resume.Kind: ResumeKind.Rearm or ResumeKind.Reemit, Approved: false };

    private static bool ParkedForDrift(WorkflowInstance instance) =>
        instance.Suspension is { Approved: false, ParkKind: ParkKind.Drift } || PendingParks(instance).Any(static p => p is { Approved: false, ParkKind: ParkKind.Drift });
}
