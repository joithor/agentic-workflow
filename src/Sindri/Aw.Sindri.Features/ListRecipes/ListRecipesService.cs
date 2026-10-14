using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>
/// The registered recipes, or the stale ones (engine spec §4.1, §6.3 'recipe list [--stale]'; README X34): each with
/// where it stands, how it was approved, why it is stale and its inputs. Stale means approved and either drifted from
/// this build's blocks or pinning a child that was revoked or superseded.
/// </summary>
/// <param name="registry">Reads where the registered recipes stand.</param>
internal sealed class ListRecipesService(RecipeRegistryReader registry) : IService<ListRecipesRequest, ListRecipesResponse>
{
    public async ValueTask<Result<ListRecipesResponse>> Handle(ListRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = await registry.Summaries(cancellationToken).ConfigureAwait(false);
        return lines.IsError
            ? lines.Error
            : new ListRecipesResponse([.. lines.Value.Where(l => !request.Stale || l is { Standing: RecipeStanding.Approved, Stale.Count: > 0 })]);
    }
}
