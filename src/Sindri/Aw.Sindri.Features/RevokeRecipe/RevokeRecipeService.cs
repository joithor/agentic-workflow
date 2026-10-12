using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>P2 contract stub (engine spec Phase 2): revokes a recipe hash through its challenge, cascades, wakes, and writes 'recipe.revoked'.</summary>
internal sealed class RevokeRecipeService : IService<RevokeRecipeRequest, RevokeRecipeResponse>
{
    public ValueTask<Result<RevokeRecipeResponse>> Handle(RevokeRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("RevokeRecipe arrives with engine P2 unit (c)");
}
