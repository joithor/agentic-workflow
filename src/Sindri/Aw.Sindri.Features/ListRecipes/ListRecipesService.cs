using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>P2 contract stub (engine spec Phase 2): the registered recipes, or the stale ones.</summary>
internal sealed class ListRecipesService : IService<ListRecipesRequest, ListRecipesResponse>
{
    public ValueTask<Result<ListRecipesResponse>> Handle(ListRecipesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ListRecipes arrives with engine P2 unit (b)");
}
