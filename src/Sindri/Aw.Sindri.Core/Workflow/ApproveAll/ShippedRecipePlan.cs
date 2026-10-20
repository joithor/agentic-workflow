namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// What the engine knows about the recipes this build ships (setup-once spec §5.3): the rows approve-all would approve, children first, the recipes
/// that need their own approval, and the digest the challenge is bound to. The API's <see cref="ApproveAllPlan"/> is this plus the sentence and the freshness.
/// </summary>
/// <param name="Recipes">The recipes approve-all approves, in the order approval happens (children first).</param>
/// <param name="OwnApproval">The previously approved recipes that gain a capability or wait on one: approved one at a time.</param>
/// <param name="Digest">The first six hex characters of the plan digest; 'approve-all &lt;digest&gt;' is the challenge's action.</param>
public sealed record ShippedRecipePlan(IReadOnlyList<ApproveAllRow> Recipes, IReadOnlyList<OwnApprovalRow> OwnApproval, string Digest)
{
    /// <summary>True when nothing waits: no recipe to approve and none needing its own approval.</summary>
    public bool IsEmpty => Recipes.Count == 0 && OwnApproval.Count == 0;
}
