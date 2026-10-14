using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The dispatch recheck's refusals (spec §5.7, §3.8; engine P6 unit (b)): 'outbox.refused' parks the instance with
/// spec-conflict of the code's kind, and '--resume' on a dispatch park re-emits the refused effect. The trigger carries
/// the effect as it was emitted (<see cref="RefusedEffect"/>), so the park names it and the resume re-sends it even
/// after the instance entered another frame.
/// </summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// 'outbox.refused': SND-RECIPE-005 parks spec-conflict (revoked), 'cancel' only; SND-RECIPE-006 parks (drift), the
    /// effect kept on its rearm for the drift approval's re-send (P2's path); SND-WF-011 parks (dispatch), whose
    /// '--resume' re-emits it. The same revoke or drift twice parks once.
    /// </summary>
    private static Decision OutboxRefusal(Deciding d, WorkflowInstance instance, OutboxRefused refused)
    {
        if (instance.Ended)
        {
            // The 'outbox.refused' row already records the refusal; an ended instance has nothing to park, and its loop
            // has exited, so 'workflow.effect-refused' (WorkflowEventType.EffectRefused) stays unused here.
            return new Absorbed(d.Key, $"{instance.Address.Value} has ended; the outbox.refused row records the refusal");
        }

        var park = RefusalPark(d, refused);
        if (park.ParkKind == ParkKind.Revoked && instance.Suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked })
        {
            return new Absorbed(d.Key, "already parked for the revoke");
        }

        return d.ChangedOrAbsorbed(d.Rules.Suspend(instance, park), instance, "already parked for drift");
    }

    private static Suspension RefusalPark(Deciding d, OutboxRefused refused)
    {
        var effect = refused.Effect;
        var recipe = d.Recipe.Id.Value;
        if (string.Equals(refused.Code, WorkflowErrors.RecipeRevoked.Code, StringComparison.Ordinal))
        {
            return SpecConflict(ParkKind.Revoked, $"{recipe} was revoked. Cancel, then start a new attempt from an approved version.",
                new ResumeAction(ResumeKind.None, Withheld: null, Gate: null, Effect: null), d.Trigger.Actor);
        }

        if (string.Equals(refused.Code, WorkflowErrors.RecipePinDrift.Code, StringComparison.Ordinal))
        {
            return SpecConflict(ParkKind.Drift, $"A block pinned by {recipe} changed since it was approved. Re-approve it; the approval resumes this instance.",
                new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, effect), d.Trigger.Actor);
        }

        return SpecConflict(ParkKind.Dispatch,
            $"{effect.Kind.Value} isn't on the profile's effect allowlist. Add it and approve the profile, then resume to send it again.",
            new ResumeAction(ResumeKind.Reemit, Withheld: null, Gate: null, effect), d.Trigger.Actor);
    }

    private static Suspension SpecConflict(ParkKind kind, string question, ResumeAction resume, string actor) =>
        new(SuspensionKind.Park, ParkReason.SpecConflict, kind, Escalation: null, question, actor, Mirrors: null, resume);

    /// <summary>
    /// '--resume' on a spec-conflict dispatch park (§3.8): the park has ended; the refused effect runs again as a new
    /// command under its identity, listed on the answer's event, and the block's existence check makes a repeat harmless.
    /// </summary>
    private static Applied Reemit(Deciding d, WorkflowInstance instance, ResumeAction resume)
    {
        var applied = d.Changed(instance);
        if (resume.Effect is not { } effect)
        {
            return applied;
        }

        var own = applied.Changes[0];
        return applied with
        {
            Changes = [own with { Event = own.Event with { Effects = [effect.Identity] } }, .. applied.Changes.Skip(1)],
            Effects = [new EffectCommand(effect.Kind, effect.Identity, CommandOrigin.Recipe, effect.With)],
        };
    }
}
