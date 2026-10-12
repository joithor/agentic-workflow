using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Stub <see cref="IApprovedRecipes"/> until P2's registry lands (engine P4 contract): '&lt;home&gt;/recipes/&lt;id&gt;.yaml'
/// read and normalized, its computed hash the approved one, and every approval check holds. P2's SQLite adapter replaces it.
/// </summary>
/// <param name="reader">The recipe reader.</param>
/// <param name="home">Where the recipes are.</param>
internal sealed class HomeApprovedRecipes(IRecipeReader reader, WorkflowsHome home) : IApprovedRecipes
{
    public async ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken)
    {
        var file = Path.Combine(home.RecipesDirectory, id.Value + ".yaml");
        if (!File.Exists(file))
        {
            return WorkflowErrors.RecipeNotFound.With($"no recipe {id.Value} under {home.RecipesDirectory} (fix: sindri recipe list)");
        }

        var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        var reading = reader.Read(text, Path.GetFileName(file));
        if (reading.Document is not { } document)
        {
            var first = reading.Findings.Count > 0 ? reading.Findings[0].ToLine() : "it doesn't bind";
            return WorkflowErrors.RecipeInvalid.With($"{file} doesn't read: {first}");
        }

        var recipe = document.Recipe with { Hash = document.Hash };
        return new ApprovedRecipe(new RecipeVersion(recipe.Id, recipe.Version, document.Hash), recipe);
    }

    public async ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
    {
        var latest = await Latest(id, cancellationToken).ConfigureAwait(false);
        return latest.IsError || latest.Value.Version.Hash == hash
            ? latest
            : WorkflowErrors.RecipeNotFound.With($"{id.Value}@{hash.Value} is not the recipe under {home.RecipesDirectory} (fix: sindri recipe list)");
    }

    public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
}
