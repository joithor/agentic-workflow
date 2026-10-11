using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The suspended-frame stack (spec §5.1, W16): at most two deep, a park or an escalation on the base frame and a
/// takeover on top of either. A pop restores the frame exactly (entry, run, child, so its inbox rows still apply)
/// and only re-arms its deadlines.
/// </summary>
internal static class FrameStack
{
    /// <summary>See <see cref="FrameRules.Suspend"/>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="outcome">The park, escalation or takeover.</param>
    /// <param name="now">The time.</param>
    /// <returns>The suspended instance.</returns>
    public static WorkflowInstance Suspend(WorkflowInstance instance, SuspendOutcome outcome, DateTimeOffset now)
    {
        var suspension = outcome.Suspension;
        if (instance.Ended)
        {
            return instance;
        }

        if (suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked })
        {
            return Replace(instance, outcome, now);
        }

        if (suspension.Kind == SuspensionKind.Takeover)
        {
            return instance.State == EngineStates.TakenOver ? instance : Push(instance, outcome, FrameDeadlines.Pause(instance.Frame.Deadlines, now), now);
        }

        if (suspension.ParkKind == ParkKind.Drift && ParkedForDrift(instance))
        {
            return instance;
        }

        return instance.Suspended.Count > 0 ? WithPending(instance, outcome) : Push(instance, outcome, FrameDeadlines.Stop(instance.Frame.Deadlines), now);
    }

    /// <summary>Pops the top frame, or the one mirroring <paramref name="child"/> when it is on top.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe; null only for a handback.</param>
    /// <param name="child">The mirrored child, or null for the top.</param>
    /// <param name="now">The time.</param>
    /// <param name="timings">The timings (the desk minimum).</param>
    /// <returns>The pop, or SND-WF-003.</returns>
    public static Result<PopResult> Pop(WorkflowInstance instance, Recipe? recipe, WorkflowInstanceId? child, DateTimeOffset now, EngineTimings timings)
    {
        var address = instance.Address.Value;
        if (instance.Ended || instance.Suspended.Count == 0)
        {
            return FrameRefusals.Refuse($"{address} has no suspended frame to resume", FrameRefusals.Status(address));
        }

        var top = instance.Suspended[^1];
        if (top.Suspension.Mirrors != child)
        {
            return FrameRefusals.Refuse(
                child is { } id ? $"the top frame of {address} doesn't mirror {id.Value}; the child's row waits until it does" : $"{address} mirrors a child; it resumes when that child changes",
                FrameRefusals.Status(address));
        }

        var restored = Restore(top, instance.Frame, recipe, now, timings);
        return new PopResult(instance with { Frame = restored, Suspended = [.. instance.Suspended.Take(instance.Suspended.Count - 1)] }, top.Resume, top.Pending);
    }

    /// <summary>All the deadlines on the instance, current and suspended.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>Every deadline.</returns>
    public static IEnumerable<Deadline> AllDeadlines(WorkflowInstance instance) =>
        instance.Frame.Deadlines.Concat(instance.Suspended.SelectMany(static s => s.Frame.Deadlines));

    private static Frame Restore(SuspendedFrame top, Frame discarded, Recipe? recipe, DateTimeOffset now, EngineTimings timings)
    {
        var frame = top.Frame;
        if (EngineStates.IsEngineOwned(frame.State))
        {
            return frame with { Deadlines = FrameDeadlines.KeepIdleArm(frame.Deadlines, discarded.Deadlines, frame.Entry) };
        }

        var deadlines = top.Suspension.Kind == SuspensionKind.Takeover
            ? FrameDeadlines.Unpause(frame.Deadlines, now)
            : FrameDeadlines.Rearm(frame.Deadlines, frame.Entry, FullTimeout(recipe, frame, timings), now);
        return frame with { Deadlines = FrameDeadlines.KeepIdleArm(deadlines, discarded.Deadlines, frame.Entry), Since = now };
    }

    private static TimeSpan? FullTimeout(Recipe? recipe, Frame frame, EngineTimings timings) =>
        recipe is not null && recipe.States.TryGetValue(frame.State, out var state) ? FrameDeadlines.FullTimeout(state, frame.Run, timings) : null;

    private static WorkflowInstance Push(WorkflowInstance instance, SuspendOutcome outcome, IReadOnlyList<Deadline> kept, DateTimeOffset now)
    {
        var below = instance.Frame with { Deadlines = kept };
        var state = outcome.Suspension.Kind switch
        {
            SuspensionKind.Escalation => EngineStates.Escalated,
            SuspensionKind.Takeover => EngineStates.TakenOver,
            SuspensionKind.Park or _ => EngineStates.Parked,
        };
        return instance with
        {
            Suspended = [.. instance.Suspended, new SuspendedFrame(below, outcome.Suspension, outcome.Resume, Pending: null)],
            Frame = new Frame(state, below.Entry, Run: null, outcome.Suspension.Mirrors, [], now),
        };
    }

    // A revoke's park replaces the stack (§5.1), ending a takeover: the base recipe frame is parked again, its deadlines stopped.
    private static WorkflowInstance Replace(WorkflowInstance instance, SuspendOutcome outcome, DateTimeOffset now)
    {
        var bottom = instance.Suspended.Count > 0 ? instance.Suspended[0].Frame : instance.Frame;
        return Push(instance with { Frame = bottom, Suspended = [] }, outcome, FrameDeadlines.Stop(bottom.Deadlines), now);
    }

    // A park arriving while parked, escalated or taken over waits on the topmost suspended frame without one (§5.1); a
    // pending park already there stands, and takes effect first.
    private static WorkflowInstance WithPending(WorkflowInstance instance, SuspendOutcome outcome)
    {
        var index = instance.Suspended.ToList().FindLastIndex(static s => s.Pending is null);
        if (index < 0)
        {
            return instance;
        }

        var stack = instance.Suspended.ToArray();
        stack[index] = stack[index] with { Pending = new PendingPark(outcome.Suspension, outcome.Resume) };
        return instance with { Suspended = stack };
    }

    private static bool ParkedForDrift(WorkflowInstance instance) =>
        instance.Suspended.Any(static s => s.Suspension.ParkKind == ParkKind.Drift || s.Pending?.Suspension.ParkKind == ParkKind.Drift);
}
