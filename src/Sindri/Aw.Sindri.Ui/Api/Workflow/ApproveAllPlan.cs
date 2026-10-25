namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// GET /sindri/workflow/recipes/approve-all: the plan one approval covers (setup-once spec §8, ApproveAllPlan). Provisional: the
/// engine's goldens (recipes-approve-all/*) are not on main yet, so the app's fixtures follow the spec's documented shape.
/// </summary>
/// <param name="Recipes">What the approval covers, in the order it happens (children first).</param>
/// <param name="OwnApproval">Previously approved workflows that gained a capability: each is approved on its own, never in this approval.</param>
/// <param name="Digest">The plan's digest; the challenge is 'approve-all' and its first six characters.</param>
/// <param name="Empty">The sentence when both lists are empty; null otherwise.</param>
internal sealed record ApproveAllPlan(IReadOnlyList<PlanRecipe> Recipes, IReadOnlyList<OwnApprovalRecipe> OwnApproval, string Digest, string? Empty);
