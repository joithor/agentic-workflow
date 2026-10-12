using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RegisterRecipe;

/// <summary>P2 contract stub (engine spec Phase 2): registers a recipe by hash and writes 'recipe.registered'.</summary>
internal sealed class RegisterRecipeService : IService<RegisterRecipeRequest, RegisterRecipeResponse>
{
    public ValueTask<Result<RegisterRecipeResponse>> Handle(RegisterRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("RegisterRecipe arrives with engine P2 unit (b)");
}
