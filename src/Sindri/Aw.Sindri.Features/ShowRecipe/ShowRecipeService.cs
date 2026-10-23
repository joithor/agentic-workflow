using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>
/// The recipe as <see cref="RecipeView"/> (API spec §3.8, E-22). With a hash it is the registered version that hash (or a prefix of 8 or
/// more digits) names, approved or not, and SND-RECIPE-002 when none does; without one it is the approved version of the id (SND-RECIPE-002
/// when the id is unknown, SND-RECIPE-003 when none is approved).
/// </summary>
/// <param name="planner">Finds the registered version a hash names.</param>
/// <param name="approved">Resolves an id to its approved version.</param>
/// <param name="json">Reads the stored recipe.</param>
/// <param name="names">The recipe's display name for the view (null serves none).</param>
/// <param name="catalog">The block descriptors that say who does each state and what its step reads and writes (null serves the view without them).</param>
internal sealed class ShowRecipeService(RecipeApprovalPlanner planner, IApprovedRecipes approved, JsonSerializerOptions json, IRecipeDisplayNames? names = null, BlockCatalog? catalog = null) : IService<ShowRecipeRequest, RecipeView>
{
    public async ValueTask<Result<RecipeView>> Handle(ShowRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Hash is null)
        {
            var latest = await approved.Latest(request.Id, cancellationToken).ConfigureAwait(false);
            return latest.IsError ? latest.Error : RecipeViews.Of(latest.Value.Recipe, latest.Value.Version, names?.Of(request.Id), catalog);
        }

        var found = await planner.Find(request.Id, request.Hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var recipe = found.Value.Read(json);
        return recipe.IsError ? recipe.Error : RecipeViews.Of(recipe.Value, new RecipeVersion(found.Value.Id, found.Value.Version, found.Value.Hash), names?.Of(request.Id), catalog);
    }
}
