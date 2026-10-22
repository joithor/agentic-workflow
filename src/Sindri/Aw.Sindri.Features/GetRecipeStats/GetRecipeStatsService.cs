using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.GetRecipeStats;

/// <summary>
/// A recipe's stats (API spec §3.8, E-24), counted from the ledger's workflow rows on each read: SND-API-013 for a window outside 7d, 30d and 90d or a
/// hash that is not a recipe hash, SND-RECIPE-002 for a recipe no one registered. A recipe some recipe starts as a child and that never started as a root
/// is child-only and has no run figures of its own. The stats read through the views.db cache (<see cref="RecipeStatsSource"/>).
/// </summary>
/// <param name="registry">Which recipes are registered, and which are started as children.</param>
/// <param name="source">The stats: cached, or counted from the ledger's workflow rows.</param>
internal sealed class GetRecipeStatsService(RecipeRegistryReader registry, RecipeStatsSource source) : IService<GetRecipeStatsRequest, RecipeStats>
{
    public async ValueTask<Result<RecipeStats>> Handle(GetRecipeStatsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = StatsWindow.Parse(request.Window);
        if (window.IsError)
        {
            return window.Error;
        }

        if (request.Hash is { } hash && !RecipeHash.IsPrefix(hash))
        {
            return SindriErrors.InvalidQuery.With("A hash is 'sha256:' and at least 8 hex digits of a recipe hash (fix: copy it from 'sindri recipe list')");
        }

        var lines = await registry.Summaries(cancellationToken).ConfigureAwait(false);
        if (lines.IsError)
        {
            return lines.Error;
        }

        if (!lines.Value.Any(l => l.Id == request.Id))
        {
            return WorkflowErrors.RecipeNotFound.With($"no recipe {request.Id.Value} is registered; sindri recipe list shows what is");
        }

        var children = await registry.ChildRecipeIds(cancellationToken).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        return await source.Of(request.Id, request.Hash, window.Value, children.Value.Contains(request.Id), cancellationToken).ConfigureAwait(false);
    }
}
