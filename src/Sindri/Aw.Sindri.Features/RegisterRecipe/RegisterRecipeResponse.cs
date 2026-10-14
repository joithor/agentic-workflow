using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.RegisterRecipe;

/// <summary>'registered workflow.step@9ab2d40c1e77 (unapproved)': what was registered and where it stands.</summary>
/// <param name="Accepted">The command id and the ledger sequence of 'recipe.registered' (the earlier one when it was already registered).</param>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Added">False when the hash was already registered.</param>
/// <param name="Standing">Unapproved, approved or revoked.</param>
public sealed record RegisterRecipeResponse(CommandAccepted Accepted, RecipeId Id, int Version, RecipeHash Hash, bool Added, RecipeStanding Standing);
