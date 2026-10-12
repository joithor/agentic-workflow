using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>P2 contract stub (engine spec Phase 2): the preview of a registered recipe.</summary>
internal sealed class ShowRecipeService : IService<ShowRecipeRequest, ShowRecipeResponse>
{
    public ValueTask<Result<ShowRecipeResponse>> Handle(ShowRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ShowRecipe arrives with engine P2 unit (b)");
}
