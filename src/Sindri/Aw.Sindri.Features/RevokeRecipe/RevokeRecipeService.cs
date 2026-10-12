using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>Red stub (engine spec Phase 2, unit (c)): the tests name the behavior; the service arrives next.</summary>
internal sealed class RevokeRecipeService : IService<RevokeRecipeRequest, RevokeRecipeResponse>
{
    public ValueTask<Result<RevokeRecipeResponse>> Handle(RevokeRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("RevokeRecipe arrives with engine P2 unit (c)");
}
