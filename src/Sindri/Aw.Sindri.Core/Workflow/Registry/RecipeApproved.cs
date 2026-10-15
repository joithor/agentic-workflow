using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>The 'recipe.approved' payload (spec §4.1), keyed 'recipe.approved:&lt;hash&gt;:after:&lt;seq&gt;' so a drift approval of the same hash is its own event.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The approved hash.</param>
/// <param name="Via">Desk or freeze.</param>
/// <param name="Base">The base of a generated recipe, or null.</param>
/// <param name="Pins">The pins recorded.</param>
/// <param name="Drift">True for a drift approval (the same hash re-approved with new pins).</param>
public sealed record RecipeApproved(RecipeId Id, int Version, RecipeHash Hash, ApprovalVia Via, RecipeRef? Base, RecipePins Pins, bool Drift);
