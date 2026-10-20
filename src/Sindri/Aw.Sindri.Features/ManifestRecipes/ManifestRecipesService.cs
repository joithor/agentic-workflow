using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Features.ManifestRecipes;

/// <summary>
/// 'recipe manifest' (setup-once spec §5.3): reads the directory as a recipe set and returns <c language="text">id@version → hash</c> for each recipe, with the registry's own hash
/// function and the eval-case count. A recipe that does not read is listed under <c language="text">Invalid</c>; a directory that cannot be read is SND-RECIPE-010.
/// </summary>
internal sealed class ManifestRecipesService(IRecipeDirectories directories, IRecipeReader reader) : IService<ManifestRecipesRequest, RecipeManifest>
{
    public async ValueTask<Result<RecipeManifest>> Handle(ManifestRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var files = await directories.Read(request.Directory, cancellationToken).ConfigureAwait(false);
        return files.IsError ? files.Error : RecipeManifests.Build(files.Value, reader);
    }
}
