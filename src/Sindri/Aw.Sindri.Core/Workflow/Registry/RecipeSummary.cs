using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One recipe line (engine spec §6.3 'recipe list'; README X34: with its inputs, for the app's start form).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Standing">Unapproved, approved or revoked.</param>
/// <param name="Via">How its latest approval was given, or null when unapproved.</param>
/// <param name="Stale">Why it is stale (empty when it isn't).</param>
/// <param name="Inputs">The facts a start supplies.</param>
public sealed record RecipeSummary(RecipeId Id, int Version, RecipeHash Hash, RecipeStanding Standing, ApprovalVia? Via, IReadOnlyList<StaleReason> Stale, IReadOnlyList<RecipeInput> Inputs);
