using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A system trigger from the registry (spec §4.1, §5.4): 'recipe.revoked:{seq}', 'recipe.drifted:{seq}' or 'recipe.approved:{seq}', always allowed.</summary>
/// <param name="Actor">'service:engine' (on behalf of the approver for an approval).</param>
/// <param name="Kind">Which change.</param>
/// <param name="Sequence">The registry event's ledger sequence.</param>
public sealed record RecipeWake(string Actor, RecipeWakeKind Kind, long Sequence) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => Kind switch
    {
        RecipeWakeKind.Revoked => TriggerNames.RecipeRevoked,
        RecipeWakeKind.Drifted => TriggerNames.RecipeDrifted,
        RecipeWakeKind.Approved or _ => TriggerNames.RecipeApproved,
    };

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"{Name}:{Sequence}");
}
