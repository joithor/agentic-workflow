using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>P2 contract stub (engine spec Phase 2): approves a recipe hash through its challenge, with pins, and writes 'recipe.approved'.</summary>
internal sealed class ApproveRecipeService : IService<ApproveRecipeRequest, ApproveRecipeResponse>
{
    public ValueTask<Result<ApproveRecipeResponse>> Handle(ApproveRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ApproveRecipe arrives with engine P2 unit (c)");
}
