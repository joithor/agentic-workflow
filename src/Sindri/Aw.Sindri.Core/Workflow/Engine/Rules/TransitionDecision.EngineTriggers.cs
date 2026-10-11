using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine triggers this unit routes to the frame and child rules (spec §3.8, §5.7): cancel, takeover, handback
/// and a child's park, resume or escalation. Answers and their §5.8 resumes, deadlines, shifts, recipe wakes, outbox
/// refusals and open-requests come with the phases that own them (P4–P6).
/// </summary>
public static partial class TransitionDecision
{
    private static Decision DecideEngineTrigger(Deciding d)
    {
        var instance = d.Input.Instance;
        if (Unapproved(d, instance, transition: null) is { } unapproved)
        {
            return unapproved;
        }

        return d.Trigger switch
        {
            CancelRequested when !instance.Ended => d.Commit(d.Rules.Cancel(instance, d.Now)),
            TakeoverRequested => d.Rules.TakeOver(instance, d.Trigger.Actor, d.Context).Match<Decision>(d.Commit, e => new Rejected(d.Key, e)),
            HandbackRequested => Handback(d, instance),
            ChildChanged change => Child(d, instance, change),
            CancelRequested => new Rejected(d.Key, NotAllowed(d.Trigger, instance)),
            _ => throw new NotSupportedException($"{d.Trigger.Name} is decided by the engine-owned rules its phase adds (P4: answers and the §5.8 resumes; P5: deadlines and shifts; P2, P6: recipe wakes and outbox refusals; UI E-6: open-requests)."),
        };
    }

    /// <summary>'handback' (§3.8): pops the taken-over frame; a park that arrived meanwhile takes effect instead.</summary>
    private static Decision Handback(Deciding d, WorkflowInstance instance)
    {
        if (instance.State != EngineStates.TakenOver)
        {
            return new Rejected(d.Key, NotAllowed(d.Trigger, instance));
        }

        var popping = d.Rules.Pop(instance, d.Recipe, child: null, d.Context);
        if (popping.IsError)
        {
            return new Rejected(d.Key, popping.Error);
        }

        var popped = popping.Value;
        return popped.Pending is { } pending
            ? d.Commit(d.Rules.Suspend(popped.Instance, new SuspendOutcome(pending.Suspension, pending.Resume), d.Now))
            : d.Commit(popped.Instance);
    }

    /// <summary>
    /// A child's park, resume or escalation (§5.7): mirrored, released, ignored, or held while the parent is busy. Any
    /// row from the child first pops the frame that mirrors it, so a new mirror replaces an old one.
    /// </summary>
    private static Decision Child(Deciding d, WorkflowInstance instance, ChildChanged change)
    {
        var effect = d.Rules.ToParent(instance, d.Recipe, change);
        return effect.Kind switch
        {
            ParentEffectKind.MirrorPark or ParentEffectKind.Escalate when effect.Suspend is { } suspend =>
                d.Commit(d.Rules.Suspend(d.Rules.Release(instance, d.Recipe, change.Child, d.Now), suspend, d.Now)),
            ParentEffectKind.Release => d.CommitOrAbsorb(d.Rules.Release(instance, d.Recipe, change.Child, d.Now), instance, "no frame mirrors that child"),
            ParentEffectKind.Hold => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"{instance.Address.Value} is busy, so the child's {change.Name} stays pending (fix: none needed; it applies when the parent can take it)")),
            ParentEffectKind.Ignore or ParentEffectKind.MirrorPark or ParentEffectKind.Escalate or _ =>
                new Absorbed(d.Key, $"the child's {change.Name} leaves the parent as it is: nothing waits on or mirrors that child, or its wait says onParked: ignore"),
        };
    }
}
