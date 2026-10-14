using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>'revoked …; N instances parked': the revocation, the cascaded ones, and the preview it was given on.</summary>
/// <param name="Accepted">The command id and the ledger sequence of the revoked hash's 'recipe.revoked'.</param>
/// <param name="Revocation">The revocation recorded.</param>
/// <param name="Cascade">The generated recipes the cascade revoked.</param>
/// <param name="Preview">The preview the challenge was issued with (its Parks are the instances woken).</param>
public sealed record RevokeRecipeResponse(CommandAccepted Accepted, RecipeRevocation Revocation, IReadOnlyList<RecipeRevocation> Cascade, RevokePreview Preview);
