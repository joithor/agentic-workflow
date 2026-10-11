using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The frame stack and the engine-owned states (spec §3.8, §5.1, §5.8, W16, W19): entering a state, pushing a park,
/// escalation or takeover, popping back exactly, re-arming deadlines, and which answer fits which suspension.
/// P3 unit (b).
/// </summary>
public static class FrameRules
{
    /// <summary>
    /// A real state entry (spec §5.1): a new frame with entry + 1, its state deadline armed at arm 1, a new run
    /// '{entry}.1' for an active state (on the desk runtime when the Step has no runtime and is deskCapable, with a
    /// timeout of at least the desk timeout), and the frame's child for a child wait. A terminal state sets the outcome.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The state entered.</param>
    /// <param name="context">The time, runtimes and timings.</param>
    /// <returns>The instance in the new frame.</returns>
    public static WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Enter");

    /// <summary>
    /// Suspends the current frame (spec §5.1): a park or escalation clears its deadlines, a takeover pauses them. The
    /// stack is at most two deep: a park or escalation sits on the base frame, a takeover on either. A park that
    /// arrives while parked or taken over becomes the suspended frame's pending park; a revoke's spec-conflict park
    /// replaces the stack, ending a takeover; a drift park while already parked for drift is absorbed.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="outcome">The park, escalation or takeover and its resume action.</param>
    /// <param name="now">The time.</param>
    /// <returns>The instance with the engine-owned frame current.</returns>
    public static WorkflowInstance Suspend(WorkflowInstance instance, SuspendOutcome outcome, DateTimeOffset now) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Suspend");

    /// <summary>
    /// Pops a suspended frame (spec §5.1): the top one, or the one mirroring <paramref name="child"/>. The frame comes
    /// back exactly; deadlines are re-armed with arm + 1, from what remained after a takeover or with the full
    /// timeout after an answer. A pop counts no round.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe (the state's timeout).</param>
    /// <param name="child">The child whose mirroring frame to pop; null pops the top.</param>
    /// <param name="now">The time.</param>
    /// <returns>The popped instance and what to run, or SND-WF-003 when nothing fits.</returns>
    public static Result<PopResult> Pop(WorkflowInstance instance, Recipe recipe, WorkflowInstanceId? child, DateTimeOffset now) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Pop");

    /// <summary>
    /// <see cref="Pop(WorkflowInstance, Recipe, WorkflowInstanceId?, DateTimeOffset)"/> under the profile's timings: a
    /// desk run's re-armed timeout is at least <see cref="EngineTimings.DeskTimeout"/> (W20). The overload with a time
    /// uses <see cref="EngineTimings.Default"/>.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe (the state's timeout).</param>
    /// <param name="child">The child whose mirroring frame to pop; null pops the top.</param>
    /// <param name="context">The time and the timings.</param>
    /// <returns>The popped instance and what to run, or SND-WF-003 when nothing fits.</returns>
    public static Result<PopResult> Pop(WorkflowInstance instance, Recipe recipe, WorkflowInstanceId? child, DecisionContext context) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Pop");

    /// <summary>
    /// Whether <paramref name="answer"/> fits the current suspension (spec §3.8, §5.8): '--grant' on rounds or
    /// no-progress, '--grant-tokens' on budget, '--text' on needs-answer or no-progress, '--accept'/'--reject' on a
    /// gate escalation, '--resume' where §3.8 lists it. Otherwise SND-WF-003, whose fix names the answer that fits.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The resume action the answer runs, or SND-WF-003.</returns>
    public static Result<ResumeAction> Fits(WorkflowInstance instance, AnswerGiven answer) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Fits");

    /// <summary>
    /// <see cref="Fits(WorkflowInstance, AnswerGiven)"/> with the bounds that need the recipe (spec W7): '--grant n'
    /// within 1..the loop's cap, and '--grant-tokens n' at most the original budget.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe (the loop's cap and the budget).</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The resume action the answer runs, or SND-WF-003.</returns>
    public static Result<ResumeAction> Fits(WorkflowInstance instance, Recipe recipe, AnswerGiven answer) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Fits");

    /// <summary>A handback (spec §3.8, W19): pops the taken-over frame back to whatever it covered, the paused deadlines re-armed from what remained.</summary>
    /// <param name="instance">The taken-over instance.</param>
    /// <param name="now">The time.</param>
    /// <returns>The popped instance and its resume action ('return', or the pending park), or SND-WF-003 when it isn't taken over.</returns>
    public static Result<PopResult> HandBack(WorkflowInstance instance, DateTimeOffset now) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.HandBack");

    /// <summary>A takeover (spec §3.8, W19): pushes a taken-over frame, pauses the deadlines, and arms a takeoverIdle deadline.</summary>
    /// <param name="instance">The instance (it may be parked or escalated).</param>
    /// <param name="actor">Who takes over.</param>
    /// <param name="context">The time and the takeover reminder.</param>
    /// <returns>The taken-over instance, or SND-WF-003 when it already is or has ended.</returns>
    public static Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor, DecisionContext context) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.TakeOver");

    /// <summary>A cancel (spec §3.8): the stack is dropped, the instance ends in 'cancelled' with outcome 'cancelled'.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="now">The time.</param>
    /// <returns>The cancelled instance.</returns>
    public static WorkflowInstance Cancel(WorkflowInstance instance, DateTimeOffset now) =>
        throw new NotSupportedException("P3 unit (b): FrameRules.Cancel");
}
