using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>
/// The registered recipes, or the stale ones (engine spec §4.1, §6.3 'recipe list [--stale]'; README X34): each with
/// where it stands, how it was approved, why it is stale and its inputs. Stale means approved and either legacy-shape (an unapproved legacy row is marked but not listed here), drifted from
/// this build's blocks or pinning a child that was revoked or superseded. Each line also carries its status word and, read from the ledger, whether it only
/// ever runs as a child and its stats at the request's window (API spec §3.8, E-25): the stats are left out for a draft and for a child-only recipe.
/// </summary>
/// <param name="registry">Reads where the registered recipes stand.</param>
/// <param name="stats">The recipes' stats, cached or counted from the ledger; null serves the lines without stats.</param>
internal sealed class ListRecipesService(RecipeRegistryReader registry, RecipeStatsSource? stats = null) : IService<ListRecipesRequest, ListRecipesResponse>
{
    public async ValueTask<Result<ListRecipesResponse>> Handle(ListRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = StatsWindow.Parse(request.Window);
        if (window.IsError)
        {
            return window.Error;
        }

        var lines = await registry.Summaries(cancellationToken).ConfigureAwait(false);
        if (lines.IsError)
        {
            return lines.Error;
        }

        var kept = lines.Value.Where(l => !request.Stale || l is { Standing: RecipeStanding.Approved, Stale.Count: > 0 }).ToList();
        if (stats is null)
        {
            return new ListRecipesResponse([.. kept.Select(WithStatus)]);
        }

        var children = await registry.ChildRecipeIds(cancellationToken).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        return await WithStats(kept, children.Value, stats, window.Value, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<ListRecipesResponse>> WithStats(List<RecipeSummary> kept, IReadOnlySet<RecipeId> children, RecipeStatsSource stats, StatsWindow window, CancellationToken cancellationToken)
    {
        var listed = new List<RecipeSummary>();
        foreach (var line in kept)
        {
            var counted = await stats.Of(line.Id, hash: null, window, children.Contains(line.Id), cancellationToken).ConfigureAwait(false);
            if (counted.IsError)
            {
                return counted.Error;
            }

            var childOnly = counted.Value.ChildOnly;
            listed.Add(WithStatus(line) with
            {
                ChildOnly = childOnly,
                Stats = line.Standing == RecipeStanding.Unapproved || childOnly ? null : RecipeListStats.Of(counted.Value),
            });
        }

        return new ListRecipesResponse(listed, window.Text);
    }

    private static RecipeSummary WithStatus(RecipeSummary line)
    {
        var status = RecipeStatus.Of(line);
        return line with { Status = status.Word, StatusDetail = status.Detail };
    }
}
