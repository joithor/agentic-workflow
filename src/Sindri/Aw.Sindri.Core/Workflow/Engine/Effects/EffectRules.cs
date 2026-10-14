using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>The pure effect decisions (engine spec §3.4, §5.7; engine P6): the dispatch recheck and the 'retry' re-emission.</summary>
public static class EffectRules
{
    /// <summary>
    /// The recheck before an effect's activity (§5.7; unit (b)): null when it may run, else the refusal's code. An
    /// engine-origin effect is exempt from both checks; a recipe effect needs its recipe's approval to hold
    /// (SND-RECIPE-005 revoked, SND-RECIPE-006 drifted) and its kind on the allowlist (SND-WF-011).
    /// </summary>
    /// <param name="origin">The effect's origin.</param>
    /// <param name="approval">The recipe's approval check now.</param>
    /// <param name="effects">The approved profile's effect settings now.</param>
    /// <param name="kind">The effect block.</param>
    /// <returns>The refusal code, or null.</returns>
    public static string? Recheck(CommandOrigin origin, ApprovalCheck approval, EffectSettings effects, BlockId kind)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _ = (origin, approval, kind);
        throw new NotSupportedException("engine P6 unit (b): EffectRules.Recheck (approval still holds, kind still allowlisted)");
    }

    /// <summary>
    /// The effects a 'retry' re-emits (§3.4; unit (a)): those of the transition that entered the instance's frame, each
    /// with the identity it had, so the effect block's existence check makes the repeat harmless.
    /// </summary>
    /// <param name="instance">The instance, in the state whose timeout retries.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The effects, in their transition's order.</returns>
    public static IReadOnlyList<EffectCommand> Reemitted(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        throw new NotSupportedException("engine P6 unit (a): EffectRules.Reemitted (retry re-emits the entering transition's effects with the same identity)");
    }
}
