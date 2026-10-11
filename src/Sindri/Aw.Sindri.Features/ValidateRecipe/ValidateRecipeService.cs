using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>Reads the recipe, validates it against the build's catalog (and the profile when there is one), and returns the findings and the hash. Red stub (P1 contract unit).</summary>
internal sealed class ValidateRecipeService : IService<ValidateRecipeRequest, ValidateRecipeResponse>
{
    public ValueTask<Result<ValidateRecipeResponse>> Handle(ValidateRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("recipe validate lands with the P1 units");
}
