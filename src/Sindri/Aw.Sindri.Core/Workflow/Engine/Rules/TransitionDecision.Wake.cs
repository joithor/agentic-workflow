using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The registry's wakes (spec §4.1, §5.8, W11; engine P6 integration): 'recipe.revoked' parks the instance at once
/// ('cancel' only), 'recipe.drifted' parks it for the re-approval, and 'recipe.approved' (a drift approval) resumes a
/// drift park and re-sends the effect the park carries, the one refused for the drift (§3.8). It never pops any other
/// park.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision Woken(Deciding d, WorkflowInstance instance, RecipeWake wake)
    {
        if (instance.Ended)
        {
            return new Absorbed(d.Key, $"{instance.Address.Value} has ended; the {wake.Name} wake has nothing to park or resume");
        }

        if (wake.Kind == RecipeWakeKind.Revoked && instance.Suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked })
        {
            return new Absorbed(d.Key, "already parked for the revoke");   // Suspend rebuilds a revoke park, so the repeat is caught here
        }

        return wake.Kind switch
        {
            RecipeWakeKind.Revoked => d.ChangedOrAbsorbed(
                d.Rules.Suspend(instance, RevokedPark(d.Recipe.Id.Value, wake.Actor)), instance, "already parked for the revoke"),
            RecipeWakeKind.Drifted => d.ChangedOrAbsorbed(
                d.Rules.Suspend(instance, DriftPark(d.Recipe.Id.Value, effect: null, wake.Actor)), instance, "already parked for drift"),
            RecipeWakeKind.Approved or _ => Reapproved(d, instance),
        };
    }

    /// <summary>A drift approval: a drift park ends and its resume runs (re-sending its effect); nothing else is resumed.</summary>
    private static Decision Reapproved(Deciding d, WorkflowInstance instance)
    {
        if (instance.Suspension is not { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift } park)
        {
            return new Absorbed(d.Key, "the approval resumes only a drift park, and this instance has none");
        }

        var ending = d.Rules.End(instance, child: null);
        return ending.IsError ? new Rejected(d.Key, ending.Error) : Resume(d, ending.Value.Instance, park.Resume);
    }
}
