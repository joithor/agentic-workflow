using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's timers (engine spec §5.5): each arming is one 'Workflow.DelayAsync' inside its own cancellation
/// scope, so cancelling the scope (a park, a takeover, leaving the entry) means the timer never fires, and every arming
/// is its own timer in history (rev 7's 'arm' counter is gone). One scope holds the state timer (the state's timeout,
/// the desk timeout, or the timer a 'then: retry' re-armed); a second holds 'takeoverIdle' while the instance is taken
/// over. A timer that ran out leaves its <see cref="DeadlineFired"/> for the loop's wait to take (<see cref="Take"/>);
/// the loop decides it like any other trigger. Expired timers fire on wake (X58). Workflow code only.
/// <para>
/// Engine P5 contract unit. Unit (a) owns the state scope (<see cref="Arm"/>, <see cref="Stop"/>, <see cref="Take"/>,
/// <see cref="FiresAt"/>) and its use in the desk and wait loops; unit (b) owns the takeover's <see cref="Pause"/> and
/// <see cref="Resume"/> and the 'takeoverIdle' scope. <see cref="Apply"/> is the one seam the loop calls with what
/// <see cref="Core.Workflow.Engine.Rules.StateTimers.Between"/> decided.
/// </para>
/// </summary>
/// <param name="actor">The actor of every <see cref="DeadlineFired"/> ('service:workflow.timers').</param>
internal sealed class TimerScopes(string actor)
{
    /// <summary>Does what <paramref name="action"/> says for the frame entry <paramref name="entry"/>.</summary>
    /// <param name="action">From StateTimers.Between.</param>
    /// <param name="entry">The frame entry the timer belongs to.</param>
    /// <returns>What a takeover left of the paused timer, for its suspension; null for every other action.</returns>
    public RemainingTimer? Apply(TimerAction action, int entry)
    {
        ArgumentNullException.ThrowIfNull(action);
        switch (action.Kind)
        {
            case TimerActionKind.Arm:
                Arm(action.Timer ?? DeadlineKind.State, entry, action.After ?? throw new ArgumentException("an arm needs its duration", nameof(action)));
                return null;
            case TimerActionKind.Stop:
                Stop();
                return null;
            case TimerActionKind.Pause:
                return Pause(entry);
            case TimerActionKind.Resume:
                Resume(action is { Timer: { } kind, After: { } left } ? new RemainingTimer(kind, left) : null, entry);
                return null;
            case TimerActionKind.Keep:
            default:
                return null;
        }
    }

    /// <summary>Cancels the running state scope, if any, and arms <paramref name="kind"/> to fire after <paramref name="after"/> in a new one. Unit (a).</summary>
    /// <param name="kind">The state timer or the retry timer.</param>
    /// <param name="entry">The frame entry it is armed in.</param>
    /// <param name="after">How long until it fires.</param>
    public void Arm(DeadlineKind kind, int entry, TimeSpan after) =>
        throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"engine P5 unit (a): arm the {kind} timer of entry {entry} for {after} ({actor})"));

    /// <summary>Cancels the running state scope with no remainder (a park, an escalation, an end). Unit (a).</summary>
    public void Stop() => throw new NotSupportedException($"engine P5 unit (a): stop the state timer ({actor})");

    /// <summary>The timer that ran out, taken: a state or retry timer of the current entry, or 'takeoverIdle'; null when none has. Unit (a).</summary>
    /// <returns>The trigger for the loop to decide.</returns>
    public DeadlineFired? Take() => throw new NotSupportedException($"engine P5 unit (a): take a fired timer ({actor})");

    /// <summary>When the running state timer fires, for status's 'timeout' line and the desk task; null when none runs. Unit (a).</summary>
    /// <returns>The time, from 'Workflow.UtcNow' at arming.</returns>
    public DateTimeOffset? FiresAt() => throw new NotSupportedException($"engine P5 unit (a): the state timer's deadline ({actor})");

    /// <summary>
    /// A takeover (spec §5.5): cancels the running state scope, measures what was left from 'Workflow.UtcNow', and arms
    /// 'takeoverIdle' (2 h) in its own scope. Unit (b).
    /// </summary>
    /// <param name="entry">The frame entry.</param>
    /// <returns>The paused timer and what was left of it; null when none was running (a takeover over a park).</returns>
    public RemainingTimer? Pause(int entry) =>
        throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"engine P5 unit (b): pause the state timer of entry {entry} on a takeover ({actor})"));

    /// <summary>A handback (spec §5.5): cancels 'takeoverIdle' and re-arms <paramref name="remaining"/>, if a timer was paused. Unit (b).</summary>
    /// <param name="remaining">What the takeover left of the paused timer.</param>
    /// <param name="entry">The frame entry.</param>
    public void Resume(RemainingTimer? remaining, int entry) =>
        throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"engine P5 unit (b): resume {remaining?.Kind.ToString() ?? "no"} timer of entry {entry} with {remaining?.Left} left ({actor})"));
}
