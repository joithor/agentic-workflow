using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>Red stub (engine spec Phase 2, unit (c)): the tests name the behavior; the service arrives next.</summary>
internal sealed class ApproveRecipeService : IService<ApproveRecipeRequest, ApproveRecipeResponse>
{
    public ValueTask<Result<ApproveRecipeResponse>> Handle(ApproveRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ApproveRecipe arrives with engine P2 unit (c)");
}
