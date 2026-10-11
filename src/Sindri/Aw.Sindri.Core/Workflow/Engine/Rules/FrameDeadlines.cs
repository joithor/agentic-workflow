using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// A frame's deadlines through push and pop (spec §5.1, W16, W19, W20). A stopped deadline keeps its id, with neither a
/// due time nor a remainder, so the arm counted per frame entry and kind keeps rising and a deadline that fired under
/// an earlier arm can never fire again.
/// </summary>
internal static class FrameDeadlines
{
    /// <summary>A real state's full timeout: its 'timeout.after', and for a desk run at least the desk timeout (W20); none for a terminal.</summary>
    /// <param name="state">The recipe state.</param>
    /// <param name="run">The frame's open run.</param>
    /// <param name="timings">The profile's timings.</param>
    /// <returns>The span, or null when nothing is armed.</returns>
    public static TimeSpan? FullTimeout(RecipeState state, OpenRun? run, EngineTimings timings)
    {
        var after = state.Kind == StateKind.Terminal ? null : state.Timeout?.After.Span;
        if (run?.Runtime != StepRuntime.Desk)
        {
            return after;
        }

        return after is { } span && span > timings.DeskTimeout ? span : timings.DeskTimeout;
    }

    /// <summary>The state deadline a real entry arms, at arm 1.</summary>
    /// <param name="entry">The frame entry.</param>
    /// <param name="full">The full timeout, or null.</param>
    /// <param name="now">The time.</param>
    /// <returns>The deadlines.</returns>
    public static IReadOnlyList<Deadline> Armed(int entry, TimeSpan? full, DateTimeOffset now) =>
        full is { } span ? [new Deadline(DeadlineId.For(entry, DeadlineKind.State, 1), now + span, Remaining: null)] : [];

    /// <summary>A park or an escalation stops every deadline (W19).</summary>
    /// <param name="deadlines">The frame's deadlines.</param>
    /// <returns>The stopped deadlines.</returns>
    public static IReadOnlyList<Deadline> Stop(IReadOnlyList<Deadline> deadlines) =>
        [.. deadlines.Select(static d => new Deadline(d.Id, Due: null, Remaining: null))];

    /// <summary>A takeover pauses the running deadlines, keeping what remained (W19).</summary>
    /// <param name="deadlines">The frame's deadlines.</param>
    /// <param name="now">The time.</param>
    /// <returns>The paused deadlines.</returns>
    public static IReadOnlyList<Deadline> Pause(IReadOnlyList<Deadline> deadlines, DateTimeOffset now) =>
        [.. deadlines.Select(d => d.Due is { } due ? new Deadline(d.Id, Due: null, due > now ? due - now : TimeSpan.Zero) : d)];

    /// <summary>After a handback, each paused deadline runs again from what remained, under arm + 1.</summary>
    /// <param name="deadlines">The frame's deadlines.</param>
    /// <param name="now">The time.</param>
    /// <returns>The re-armed deadlines.</returns>
    public static IReadOnlyList<Deadline> Unpause(IReadOnlyList<Deadline> deadlines, DateTimeOffset now) =>
        [.. deadlines.Select(d => d.Remaining is { } left ? new Deadline(Next(d.Id), now + left, Remaining: null) : d)];

    /// <summary>After an answer, a fresh state deadline with the full timeout under arm + 1; the other kinds stay stopped.</summary>
    /// <param name="deadlines">The frame's (stopped) deadlines.</param>
    /// <param name="entry">The frame entry.</param>
    /// <param name="full">The state's full timeout, or null.</param>
    /// <param name="now">The time.</param>
    /// <returns>The re-armed deadlines.</returns>
    public static IReadOnlyList<Deadline> Rearm(IReadOnlyList<Deadline> deadlines, int entry, TimeSpan? full, DateTimeOffset now)
    {
        var others = deadlines.Where(static d => d.Id.Kind != DeadlineKind.State);
        if (full is not { } span)
        {
            return [.. others];
        }

        var arm = NextArm(deadlines, entry, DeadlineKind.State);
        return [.. others, new Deadline(DeadlineId.For(entry, DeadlineKind.State, arm), now + span, Remaining: null)];
    }

    /// <summary>
    /// Carries the highest takeoverIdle arm of the frame a pop discards into the restored frame, stopped, so the next
    /// takeover in the same entry arms the next one.
    /// </summary>
    /// <param name="restored">The restored frame's deadlines.</param>
    /// <param name="discarded">The discarded frame's deadlines.</param>
    /// <param name="entry">The entry both frames share.</param>
    /// <returns>The restored deadlines with at most one takeoverIdle, stopped.</returns>
    public static IReadOnlyList<Deadline> KeepIdleArm(IReadOnlyList<Deadline> restored, IReadOnlyList<Deadline> discarded, int entry)
    {
        var arm = NextArm(restored.Concat(discarded), entry, DeadlineKind.TakeoverIdle) - 1;
        if (arm == 0)
        {
            return restored;
        }

        return [.. restored.Where(static d => d.Id.Kind != DeadlineKind.TakeoverIdle), new Deadline(DeadlineId.For(entry, DeadlineKind.TakeoverIdle, arm), Due: null, Remaining: null)];
    }

    /// <summary>The next arm of <paramref name="kind"/> in <paramref name="entry"/>: one more than the highest seen.</summary>
    /// <param name="deadlines">Every deadline that may carry the entry.</param>
    /// <param name="entry">The frame entry.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The arm, from 1.</returns>
    public static int NextArm(IEnumerable<Deadline> deadlines, int entry, DeadlineKind kind) =>
        deadlines.Where(d => d.Id.Entry == entry && d.Id.Kind == kind).Select(static d => d.Id.Arm).DefaultIfEmpty(0).Max() + 1;

    private static DeadlineId Next(DeadlineId id) => DeadlineId.For(id.Entry, id.Kind, id.Arm + 1);
}
