using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// 'GET /workflow/recipes/approve-all' (setup-once spec §7.2): what one hold would approve. The plan carries no eval result: the shipped recipes
/// passed theirs at publish.
/// </summary>
/// <param name="Recipes">The recipes the hold approves, children first.</param>
/// <param name="OwnApproval">The previously approved recipes approved one at a time.</param>
/// <param name="Digest">Six hex characters; the challenge is 'approve-all &lt;digest&gt;'.</param>
/// <param name="Empty">The sentence when both lists are empty, else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ApproveAllPlan(IReadOnlyList<ApproveAllRow> Recipes, IReadOnlyList<OwnApprovalRow> OwnApproval, string Digest, string? Empty, AsOf AsOf)
{
    /// <summary>True when there is no recipe a hold would approve (a recipe needing its own approval is approved one at a time): a start answers the empty result.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NothingToHold => Recipes.Count == 0;

    /// <summary>The sentence an empty plan carries.</summary>
    public const string EmptySentence = "Every recipe is approved.";

    /// <summary>The API's plan for the engine's <paramref name="plan"/>.</summary>
    /// <param name="plan">What the engine knows.</param>
    /// <param name="asOf">The views' freshness.</param>
    /// <returns>The plan.</returns>
    public static ApproveAllPlan Of(ShippedRecipePlan plan, AsOf asOf)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ApproveAllPlan(plan.Recipes, plan.OwnApproval, plan.Digest, plan.IsEmpty ? EmptySentence : null, asOf);
    }
}
