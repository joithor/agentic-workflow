namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// What the engine knows about the recipes this build ships (setup-once spec §5.3): the rows approve-all would approve, children first, and the recipes that need their own
/// approval. The digest the challenge is bound to is computed here from the rows, so no implementation can bind a hold to less than the owner saw. The API's
/// <see cref="ApproveAllPlan"/> is this plus the sentence and the freshness.
/// </summary>
/// <param name="Recipes">The recipes approve-all approves, in the order approval happens (children first).</param>
/// <param name="OwnApproval">The previously approved recipes that gain a capability or wait on one: approved one at a time.</param>
public sealed record ShippedRecipePlan(IReadOnlyList<ApproveAllRow> Recipes, IReadOnlyList<OwnApprovalRow> OwnApproval)
{
    /// <summary>The first six hex characters of the plan digest; 'approve-all &lt;digest&gt;' is the challenge's action.</summary>
    public string Digest => ApproveAllDigest.Of(Recipes);

    /// <summary>True when nothing waits: no recipe to approve and none needing its own approval.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Recipes.Count == 0 && OwnApproval.Count == 0;

    /// <summary>True when there is no recipe a hold would approve (a recipe that needs its own approval is approved one at a time): no challenge is issued and no job starts.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NothingToHold => Recipes.Count == 0;
}
