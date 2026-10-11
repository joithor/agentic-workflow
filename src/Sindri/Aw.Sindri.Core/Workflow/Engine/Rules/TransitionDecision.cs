using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine's one decision function (spec §3.5, §5.3–§5.5, W12–W14): given the row, its recipe, a trigger and the
/// matched transition's gate answers, what the Commit writes. Pure: everything it reads is in its input. It applies
/// the §5.3 validation steps it can see (run token and state, approval), matches the transition, and hands
/// refusals to <see cref="RefusalRules"/>, frames to <see cref="FrameRules"/>, loops and budgets to
/// <see cref="LoopRules"/> and <see cref="BudgetRules"/>, child rows to <see cref="ChildPropagation"/>, and builds
/// each event's post-state delta with <see cref="EventFold.Delta"/>. P3 unit (a).
/// </summary>
public static class TransitionDecision
{
    /// <summary>
    /// A new instance (spec §5.1, §5.2): the row in the recipe's initial state as entry 1, with its deadlines armed
    /// and its run opened (an active state) or its child created (a child wait), and its workflow.started event.
    /// </summary>
    /// <param name="request">The new instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="context">The time, epoch, catalog, runtimes, children and timings.</param>
    /// <returns><see cref="Applied"/> with the started row (and a created child's), or <see cref="Rejected"/>.</returns>
    public static Decision Start(StartRequest request, Recipe recipe, DecisionContext context) =>
        Start(request, recipe, context, EngineRules.Default);

    /// <summary>
    /// The transition <paramref name="trigger"/> selects from the current state (spec §3.5): the first in file order
    /// whose 'on' is the trigger and whose 'when' holds. A report for another run is <see cref="MatchMiss.StaleRun"/>;
    /// an engine trigger is <see cref="MatchMiss.EngineTrigger"/>.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The match, so the caller evaluates exactly its gates and evidence.</returns>
    public static TransitionMatch Match(WorkflowInstance instance, Recipe recipe, Trigger trigger) =>
        throw new NotSupportedException("P3 unit (a): TransitionDecision.Match");

    /// <summary>Decides one trigger (spec §5.3–§5.5).</summary>
    /// <param name="input">The row, recipe, trigger, gate answers and context.</param>
    /// <returns>Applied, refused, absorbed or rejected.</returns>
    public static Decision Decide(DecisionInput input) => Decide(input, EngineRules.Default);

    /// <summary><see cref="Start(StartRequest, Recipe, DecisionContext)"/> through <paramref name="rules"/>.</summary>
    internal static Decision Start(StartRequest request, Recipe recipe, DecisionContext context, IEngineRules rules) =>
        throw new NotSupportedException("P3 unit (a): TransitionDecision.Start");

    /// <summary><see cref="Decide(DecisionInput)"/> through <paramref name="rules"/>.</summary>
    internal static Decision Decide(DecisionInput input, IEngineRules rules) =>
        throw new NotSupportedException("P3 unit (a): TransitionDecision.Decide");
}
