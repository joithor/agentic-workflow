using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The timer model the interpreter follows (engine spec §3.4, §5.5, W20, X58), pure so the workflow and its unit tests
/// call the same code. Every 'active' and 'wait' state arms its timeout as a 'Workflow.DelayAsync' inside its own
/// cancellation scope: the state timeout, the one 'then: retry' re-arms once in the same entry, and the desk timeout
/// (the state's timeout raised to 'workflow.deskTimeout', W20). A park or escalation stops it; a takeover pauses it,
/// keeping <see cref="RemainingTimer"/> on the suspension, and arms 'takeoverIdle'; a handback re-arms it with what was
/// left; an answer to a park arms the full timeout. Expired timers fire on wake (X58): Temporal's timers are server-side
/// wall clock. Engine P5 contract unit: unit (a) owns the state timers and 'retry', unit (b) the takeover's pause,
/// resume and 'takeoverIdle'.
/// </summary>
public static class StateTimers
{
    /// <summary>The takeover's idle reminder (spec §5.5): it notifies once ('hand back {item}?') and clears; it never hands back.</summary>
    public static TimeSpan TakeoverIdle { get; } = TimeSpan.FromHours(2);

    /// <summary>
    /// How long <paramref name="state"/>'s timer runs when armed in full (spec §3.4, W20): its 'timeout.after', raised to
    /// <paramref name="deskFloor"/> while a desk run is open; none for a terminal state. Unit (a).
    /// </summary>
    /// <param name="state">The recipe state.</param>
    /// <param name="run">The frame's open run, for an active state.</param>
    /// <param name="deskFloor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The timeout; null when the state arms none.</returns>
    public static TimeSpan? Timeout(RecipeState state, OpenRun? run, TimeSpan deskFloor)
    {
        ArgumentNullException.ThrowIfNull(state);
        throw new NotSupportedException($"engine P5 unit (a): the state timeout of a {state.Kind} state (run {run?.Token.Value ?? "none"}, desk floor {deskFloor})");
    }

    /// <summary>
    /// What the interpreter does with its timers after a decided change from <paramref name="before"/> to
    /// <paramref name="after"/> (spec §5.5): a takeover's pause or a handback's resume (unit (b)) first, else what a new
    /// entry, a new run, a park, an answer or an end asks for (unit (a)).
    /// </summary>
    /// <param name="before">The instance before the change.</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="recipe">The recipe, for the entered state's timeout.</param>
    /// <param name="deskFloor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The action.</returns>
    public static TimerAction Between(WorkflowInstance before, WorkflowInstance after, Recipe recipe, TimeSpan deskFloor) =>
        Takeovers(before, after) ?? Entries(before, after, recipe, deskFloor);

    /// <summary>
    /// A takeover of a running timer pauses it; a handback resumes the timer its suspension kept (none when it covered a
    /// park); any other change is not a takeover's (null). Unit (b).
    /// </summary>
    internal static TimerAction? Takeovers(WorkflowInstance before, WorkflowInstance after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        throw new NotSupportedException($"engine P5 unit (b): the takeover timers of {after.Address.Value} ({before.State} -> {after.State})");
    }

    /// <summary>
    /// A new entry or run arms the entered state's full timeout; a park, escalation or end stops it; an answer that ends
    /// a park arms it in full ('rearm'); anything else keeps it. Unit (a).
    /// </summary>
    internal static TimerAction Entries(WorkflowInstance before, WorkflowInstance after, Recipe recipe, TimeSpan deskFloor)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(recipe);
        throw new NotSupportedException($"engine P5 unit (a): the state timers of {after.Address.Value} ({before.State} -> {after.State}, {recipe.Id.Value}, desk floor {deskFloor})");
    }
}
