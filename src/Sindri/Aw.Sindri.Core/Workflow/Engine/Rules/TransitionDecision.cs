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
public static partial class TransitionDecision
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
    public static TransitionMatch Match(WorkflowInstance instance, Recipe recipe, Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(trigger);
        return MatchFrom(Unmirrored(instance, trigger), recipe, trigger);
    }

    /// <summary>Decides one trigger (spec §5.3–§5.5).</summary>
    /// <param name="input">The row, recipe, trigger, gate answers and context.</param>
    /// <returns>Applied, refused, absorbed or rejected.</returns>
    public static Decision Decide(DecisionInput input) => Decide(input, EngineRules.Default);

    /// <summary><see cref="Decide(DecisionInput)"/> through <paramref name="rules"/>.</summary>
    internal static Decision Decide(DecisionInput input, IEngineRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        var deciding = new Deciding(input, rules, input.Trigger.KeyFor(input.Instance.Id));
        if (input.Context.Recorded.Contains(deciding.Key))
        {
            return new Absorbed(deciding.Key, "already applied");
        }

        return input.Trigger is StepCompleted or StepFailed or WaitSatisfied or CommandFired
            ? DecideRecipeTrigger(deciding)
            : DecideEngineTrigger(deciding);
    }

    /// <summary>A recipe trigger (§5.3 steps 4 and 6, §3.5): validate, check the approval, spend, then match and apply or refuse.</summary>
    private static Decision DecideRecipeTrigger(Deciding d)
    {
        var instance = Release(d);
        var match = MatchFrom(instance, d.Recipe, d.Trigger);
        if (Invalid(d, instance, match) is { } invalid)
        {
            return new Rejected(d.Key, invalid);
        }

        if (Unapproved(d, instance, match.Transition) is { } unapproved)
        {
            return unapproved;
        }

        var spend = Spend(d, instance);
        if (spend.Park is { } budget)
        {
            return d.Commit(d.Rules.Suspend(instance with { Spent = spend.Spent }, budget, d.Now));
        }

        var spent = instance with { Spent = spend.Spent };
        return match.Transition is { } index ? Transition(d, spent, index) : Unmatched(d, spent);
    }

    /// <summary>A child's row first pops the parent's frame that mirrors that child (§5.7), so a child that ends releases its parent.</summary>
    private static WorkflowInstance Release(Deciding d) =>
        d.Trigger is WaitSatisfied { Source: ChildWaitSource child } && Mirrors(d.Input.Instance, child.Child)
            ? d.Rules.Release(d.Input.Instance, d.Recipe, child.Child, d.Now)
            : d.Input.Instance;

    private static bool Mirrors(WorkflowInstance instance, WorkflowInstanceId child) =>
        instance.Suspended.Any(s => s.Suspension.Mirrors == child);

    /// <summary>Allowed, but no 'when' holds (§3.5): a step.failed parks with needs-answer; any other trigger is SND-WF-003.</summary>
    private static Decision Unmatched(Deciding d, WorkflowInstance instance)
    {
        if (d.Trigger is not StepFailed failed)
        {
            return new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"no transition from '{instance.State}' on {d.Trigger.Name} holds for this {Describe(d.Trigger)} (fix: see allowedCommands in status)"));
        }

        var step = d.Recipe.States[instance.State].Step?.Use.Value ?? instance.State;
        var park = new SuspendOutcome(
            new Suspension(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, $"{step} failed: {failed.Reason}", failed.Actor, Mirrors: null),
            new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effect: null));
        return d.Commit(d.Rules.Suspend(instance, park, d.Now));
    }

    /// <summary>One trigger being decided, with the rules it hands work to.</summary>
    /// <param name="Input">What the decision reads.</param>
    /// <param name="Rules">The frame, loop, budget, child and fold rules.</param>
    /// <param name="Key">The trigger's key.</param>
    private sealed partial record Deciding(DecisionInput Input, IEngineRules Rules, TriggerKey Key)
    {
        public Recipe Recipe => Input.Recipe;

        public Trigger Trigger => Input.Trigger;

        public DecisionContext Context => Input.Context;

        public DateTimeOffset Now => Input.Context.Now;

        /// <summary>The scope a state entry runs in.</summary>
        public Scope Scope => new(Rules, Context, Recipe, Key, Trigger.Actor);
    }

    /// <summary>What entering a state reads: the rules, the context, the recipe, and the key and actor a created child keeps.</summary>
    /// <param name="Rules">The rules.</param>
    /// <param name="Context">The context.</param>
    /// <param name="Recipe">The recipe.</param>
    /// <param name="Key">The trigger's key.</param>
    /// <param name="Actor">The trigger's actor.</param>
    private sealed record Scope(IEngineRules Rules, DecisionContext Context, Recipe Recipe, TriggerKey Key, string Actor);
}
