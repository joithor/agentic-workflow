using System.Globalization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckMap;

/// <summary>
/// Doctor 'context:map' (spec §8.7): warns with no map or with orphaned overrides, fails when the store can't open.
/// How many commits the current map is behind HEAD is unknown until a build records its commit (S-4).
/// </summary>
internal sealed class CheckMapService(IGraphStore store) : IService<CheckMapRequest, MapHealth>
{
    private const string Name = "context:map";

    public async ValueTask<Result<MapHealth>> Handle(CheckMapRequest request, CancellationToken cancellationToken)
    {
        var current = await store.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return Fail(current.Error);
        }

        if (current.Value is not { } version)
        {
            return new MapHealth(Name, MapHealthState.Warn, "no map yet", "context map import <file> (or context map build)");
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return Fail(graph.IsError ? graph.Error : overrides.Error);
        }

        var detail = string.Create(CultureInfo.InvariantCulture,
            $"map v{version.Number} current ({version.Repo}, {graph.Value.Nodes.Count} nodes, {graph.Value.Edges.Count} edges); commits behind HEAD: unknown until a build records its commit");
        var orphaned = OverrideApplier.Apply(graph.Value, overrides.Value).Orphaned.Count;
        return orphaned == 0
            ? new MapHealth(Name, MapHealthState.Ok, detail, Fix: null)
            : new MapHealth(Name, MapHealthState.Warn, string.Create(CultureInfo.InvariantCulture, $"{detail}; {orphaned} orphaned override(s)"),
                "import a map that has their nodes again, or leave them for map review (S-4)");
    }

    private static MapHealth Fail(Error error) =>
        new(Name, MapHealthState.Fail, $"{error.Code}: {error.Message}", "check $AW_SINDRI_HOME/context permissions; never delete context.db");
}
