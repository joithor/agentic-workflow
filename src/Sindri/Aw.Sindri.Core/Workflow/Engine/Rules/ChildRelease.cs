using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Pops the frame mirroring a child (spec §5.7: any row from the child pops it first), wherever it sits on the stack,
/// and clears a pending park that mirrors it.
/// </summary>
internal static class ChildRelease
{
    /// <summary>See <see cref="ChildPropagation.Release"/>; the same instance when nothing mirrors <paramref name="child"/>.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="recipe">The parent's recipe (the restored wait's timeout).</param>
    /// <param name="child">The child the row is from.</param>
    /// <param name="now">The time.</param>
    /// <returns>The parent with no frame mirroring that child.</returns>
    public static WorkflowInstance Release(WorkflowInstance parent, Recipe recipe, WorkflowInstanceId child, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(recipe);
        var stack = ClearPending(parent.Suspended, child);
        var index = stack.FindLastIndex(s => s.Suspension.Mirrors == child);
        if (index < 0)
        {
            return stack.SequenceEqual(parent.Suspended) ? parent : parent with { Suspended = stack };
        }

        var mirror = stack[index];
        var above = index + 1 < stack.Count;
        var discarded = above ? stack[index + 1].Frame : parent.Frame;
        var (kept, top) = mirror.Pending is { } pending
            ? Repark(mirror, pending, discarded, now)
            : (null, Restore(mirror.Frame, discarded, recipe, now, paused: above));
        var below = stack.Take(index).Concat(kept is null ? [] : [kept]);
        return above
            ? parent with { Suspended = [.. below, stack[index + 1] with { Frame = top }, .. stack.Skip(index + 2)] }
            : parent with { Suspended = [.. below], Frame = top };
    }

    // A park that arrived while the child was mirrored takes effect in place of the restore (§5.1): the frame below is parked again.
    private static (SuspendedFrame Kept, Frame Top) Repark(SuspendedFrame mirror, PendingPark pending, Frame discarded, DateTimeOffset now)
    {
        var frame = mirror.Frame with { Deadlines = KeepIdleArm(mirror.Frame.Deadlines, discarded.Deadlines, mirror.Frame.Entry) };
        var state = pending.Suspension.Kind == SuspensionKind.Escalation ? EngineStates.Escalated : EngineStates.Parked;
        return (new SuspendedFrame(frame, pending.Suspension, pending.Resume, Pending: null), new Frame(state, frame.Entry, Run: null, pending.Suspension.Mirrors, [], now));
    }

    // The wait runs again under a fresh state deadline (arm + 1, the full timeout), paused while a takeover sits above it.
    private static Frame Restore(Frame frame, Frame discarded, Recipe recipe, DateTimeOffset now, bool paused)
    {
        if (EngineStates.IsEngineOwned(frame.State))
        {
            return frame with { Deadlines = KeepIdleArm(frame.Deadlines, discarded.Deadlines, frame.Entry) };
        }

        var full = recipe.States.TryGetValue(frame.State, out var state) ? state.Timeout?.After.Span : null;
        IReadOnlyList<Deadline> deadlines = [.. frame.Deadlines.Where(static d => d.Id.Kind != DeadlineKind.State)];
        if (full is { } span)
        {
            var id = DeadlineId.For(frame.Entry, DeadlineKind.State, NextArm(frame.Deadlines, frame.Entry, DeadlineKind.State));
            deadlines = [.. deadlines, paused ? new Deadline(id, Due: null, span) : new Deadline(id, now + span, Remaining: null)];
        }

        return frame with { Deadlines = KeepIdleArm(deadlines, discarded.Deadlines, frame.Entry), Since = now };
    }

    // The highest takeoverIdle arm of the discarded mirror frame carries into the restored one, stopped, so the next
    // takeover in the same entry arms the next one (as FrameDeadlines.KeepIdleArm, P3 unit (b)).
    private static IReadOnlyList<Deadline> KeepIdleArm(IReadOnlyList<Deadline> restored, IReadOnlyList<Deadline> discarded, int entry)
    {
        var arm = NextArm(restored.Concat(discarded), entry, DeadlineKind.TakeoverIdle) - 1;
        return arm == 0
            ? restored
            : [.. restored.Where(static d => d.Id.Kind != DeadlineKind.TakeoverIdle), new Deadline(DeadlineId.For(entry, DeadlineKind.TakeoverIdle, arm), Due: null, Remaining: null)];
    }

    private static int NextArm(IEnumerable<Deadline> deadlines, int entry, DeadlineKind kind) =>
        deadlines.Where(d => d.Id.Entry == entry && d.Id.Kind == kind).Select(static d => d.Id.Arm).DefaultIfEmpty(0).Max() + 1;

    private static List<SuspendedFrame> ClearPending(IReadOnlyList<SuspendedFrame> stack, WorkflowInstanceId child) =>
        [.. stack.Select(s => s.Pending?.Suspension.Mirrors == child ? s with { Pending = null } : s)];
}
