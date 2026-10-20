using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One recipe line (engine spec §6.3 'recipe list'; README X34: with its inputs, for the app's start form).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id (setup-once spec §7.7).</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Standing">Unapproved, approved or revoked.</param>
/// <param name="Via">How its latest approval was given, or null when unapproved.</param>
/// <param name="Stale">Why it is stale (empty when it isn't).</param>
/// <param name="Inputs">The facts a start supplies (the first is the subject), with their types and kinds.</param>
/// <param name="Urgency">The recipe's urgency: incident, triage or work.</param>
/// <param name="Outputs">What the recipe produces for a human, each with the state it comes from.</param>
/// <param name="OrderedBy">'path', or 'transition-list' when 'initial' has more than one first target (spec §3.10).</param>
/// <param name="States">Every state with its path order and branch (spec §3.10, UI E-20); computed, never hashed.</param>
/// <param name="Loops">The loop edges of the path walk (spec §3.10).</param>
/// <param name="Name">The recipe's name template as written, or null (spec §3.11).</param>
public sealed record RecipeSummary(RecipeId Id, string DisplayName, int Version, RecipeHash Hash, RecipeStanding Standing, ApprovalVia? Via, IReadOnlyList<StaleReason> Stale, IReadOnlyList<RecipeInput> Inputs, string Urgency, IReadOnlyList<RecipeOutput> Outputs, string OrderedBy, IReadOnlyList<StatePlace> States, IReadOnlyList<PathLoop> Loops, string? Name = null);
