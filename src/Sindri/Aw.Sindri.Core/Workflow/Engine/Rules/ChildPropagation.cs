using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Children both ways (spec §5.7, W17): a child created in its parent's Commit, its changes propagated to the
/// parent's frames, answers forwarded to it, and a parent's cancel cascading. P3 unit (d).
/// </summary>
public static class ChildPropagation
{
    /// <summary>
    /// The child a 'waitFor: { child }' creates (spec §5.7): from the parent's pinned hash, its inputs from the wait's
    /// '{ fact }' and literals, its address '{parent}/{recipe name}.{parent entry}', its id derived from the parent's
    /// trigger key, and the parent trigger's actor.
    /// </summary>
    /// <param name="parent">The parent, in the frame that entered the wait.</param>
    /// <param name="wait">The child wait.</param>
    /// <param name="key">The parent's trigger key.</param>
    /// <param name="actor">The parent trigger's actor.</param>
    /// <returns>The child's start.</returns>
    /// <exception cref="NotSupportedException">Always: the child's version is the resolved recipe's, which this overload doesn't have.</exception>
    public static StartRequest Create(WorkflowInstance parent, ChildWait wait, TriggerKey key, string actor) =>
        throw new NotSupportedException("ChildPropagation.Create needs the resolved child recipe for the child's version; call Create(parent, wait, child, key, actor)");

    /// <summary>
    /// <see cref="Create(WorkflowInstance, ChildWait, TriggerKey, string)"/> with the version of the child recipe the
    /// resolver found; the hash stays the parent's pin.
    /// </summary>
    /// <param name="parent">The parent, in the frame that entered the wait.</param>
    /// <param name="wait">The child wait.</param>
    /// <param name="child">The child recipe as resolved.</param>
    /// <param name="key">The parent's trigger key.</param>
    /// <param name="actor">The parent trigger's actor.</param>
    /// <returns>The child's start.</returns>
    public static StartRequest Create(WorkflowInstance parent, ChildWait wait, ChildRecipe child, TriggerKey key, string actor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(child);
        var pin = new PinnedRecipe(wait.Recipe, child.Recipe.Version, parent.Recipe.Children[wait.Recipe], new Dictionary<RecipeId, RecipeHash>());
        return new StartRequest(
            key,
            ChildInputs.IdFor(key),
            parent.Address.Child(wait.Recipe, parent.Frame.Entry),
            pin,
            parent.Item,
            parent.Attempt,
            parent.Id,
            ChildInputs.From(parent, wait),
            actor,
            TakenOver: false);
    }

    /// <summary>
    /// What a child's park, resume or escalation does to the parent (spec §5.7): a park propagates as a mirror with
    /// 'onParked: propagate', a resume pops the mirror, an escalation always pushes 'escalated (child)'; a busy
    /// parent holds the row.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="recipe">The parent's recipe (the wait's onParked).</param>
    /// <param name="change">The child's change.</param>
    /// <returns>The parent's effect.</returns>
    public static ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(change);
        var wait = WaitOn(parent, recipe, change.Child);
        if (wait is null)
        {
            return Effect(ParentEffectKind.Ignore);
        }

        var mirrors = parent.Suspended.Count > 0 && parent.Suspended[^1].Suspension.Mirrors == change.Child;
        if (change.Change == ChildChange.Resumed)
        {
            return Effect(mirrors ? ParentEffectKind.Release : Mirrored(parent, change.Child) ? ParentEffectKind.Hold : ParentEffectKind.Ignore);
        }

        if (change.Change == ChildChange.Parked && wait.OnParked == OnParked.Ignore)
        {
            return Effect(ParentEffectKind.Ignore);
        }

        // A mirror of this same child is popped before the new one is pushed (Release); anything else on the stack is the parent's own.
        if (parent.Suspended.Any(s => s.Suspension.Mirrors != change.Child))
        {
            return Effect(ParentEffectKind.Hold);
        }

        return change.Change == ChildChange.Parked
            ? new ParentEffect(ParentEffectKind.MirrorPark, Mirror(change, SuspensionKind.Park, escalation: null))
            : new ParentEffect(ParentEffectKind.Escalate, Mirror(change, SuspensionKind.Escalation, EscalationKind.Child));
    }

    /// <summary>Any row from a child first pops the frame mirroring that child (spec §5.7), so a child that ends releases its parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="recipe">The parent's recipe.</param>
    /// <param name="child">The child the row is from.</param>
    /// <param name="now">The time.</param>
    /// <returns>The parent with no frame mirroring that child.</returns>
    public static WorkflowInstance Release(WorkflowInstance parent, Recipe recipe, WorkflowInstanceId child, DateTimeOffset now) =>
        ChildRelease.Release(parent, recipe, child, now);

    /// <summary>Where an answer to the parent goes (spec §5.7): to the child its frame mirrors; cancel and takeover act on the parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="trigger">The trigger given to the parent.</param>
    /// <returns>The child to forward it to; null when the parent takes it.</returns>
    public static WorkflowInstanceId? ForwardTo(WorkflowInstance parent, Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return trigger is AnswerGiven && parent.Suspended.Count > 0 ? parent.Suspended[^1].Suspension.Mirrors : null;
    }

    /// <summary>
    /// The answer forwarded to the child (spec §5.7, W17): the child's own trigger under the nonce minted for the child,
    /// keeping the actor who answered the parent, with 'onBehalfOf' naming the parent and that actor.
    /// </summary>
    /// <param name="parent">The parent whose frame mirrors the child.</param>
    /// <param name="answer">The answer given to the parent.</param>
    /// <param name="stamp">The nonce minted for the child and the argument digest.</param>
    /// <returns>The child's answer.</returns>
    public static AnswerGiven Forward(WorkflowInstance parent, AnswerGiven answer, CommandStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(answer);
        return answer with { Stamp = stamp, Forwarded = new OnBehalfOf(parent.Id, answer.Actor) };
    }

    /// <summary>The children a parent's cancel cancels (spec §5.7): every one, whatever its state.</summary>
    /// <param name="parent">The cancelled parent.</param>
    /// <param name="children">The parent's children as the store lists them.</param>
    /// <returns>The children still to cancel.</returns>
    public static IReadOnlyList<WorkflowInstanceId> Cascade(WorkflowInstance parent, IReadOnlyList<WorkflowInstance> children)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(children);
        return [.. children.Where(c => c.Parent == parent.Id && !c.Ended).Select(static c => c.Id)];
    }

    /// <summary>The cancel a cascaded child gets (spec §5.7): origin parent, keyed by the parent and its version after the cancel; exempt from the approval check, so a drifted child is cancelled too.</summary>
    /// <param name="parent">The cancelled parent, after its cancel.</param>
    /// <param name="actor">The service that carries the parent's cancel.</param>
    /// <returns>The child's cancel.</returns>
    public static CancelRequested CancelFor(WorkflowInstance parent, string actor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new CancelRequested(actor, CancelOrigin.Parent, Stamp: null, parent.Version, parent.Id);
    }

    // The wait on 'child' of the parent's recipe frame (the bottom of the stack); null when the parent ended or waits on another child.
    private static ChildWait? WaitOn(WorkflowInstance parent, Recipe recipe, WorkflowInstanceId child)
    {
        var frame = parent.Suspended.Count > 0 ? parent.Suspended[0].Frame : parent.Frame;
        if (parent.Ended || frame.Child != child)
        {
            return null;
        }

        return recipe.States.TryGetValue(frame.State, out var state) ? state.WaitFor?.Child : null;
    }

    private static bool Mirrored(WorkflowInstance parent, WorkflowInstanceId child) => parent.Suspended.Any(s => s.Suspension.Mirrors == child);

    private static ParentEffect Effect(ParentEffectKind kind) => new(kind, Suspend: null);

    private static SuspendOutcome Mirror(ChildChanged change, SuspensionKind kind, EscalationKind? escalation) => new(
        new Suspension(kind, kind == SuspensionKind.Park ? change.Reason : null, ParkKind: null, escalation, change.Question ?? string.Empty, change.Actor, change.Child),
        new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effect: null));
}
