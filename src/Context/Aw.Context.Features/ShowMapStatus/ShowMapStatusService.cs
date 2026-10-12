using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version, its counts, its orphaned overrides and how many earlier versions are kept; a session sees its repo's scope only.</summary>
internal sealed class ShowMapStatusService(IGraphStore store) : IService<ShowMapStatusRequest, MapStatusView>
{
    public async ValueTask<Result<MapStatusView>> Handle(ShowMapStatusRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Reader) is { } refused)
        {
            return refused;
        }

        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError || versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current) is not { } version)
        {
            return versions.IsError ? versions.Error : ContextErrors.NoMap.With("there is no map yet (fix: context map import <file>, or context map build)");
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return graph.IsError ? graph.Error : overrides.Error;
        }

        var visible = MapAccess.Visible(graph.Value, overrides.Value, request.Reader);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var (shown, layer) = visible.Value;
        var orphaned = OverrideApplier.Apply(shown, layer).Orphaned.Count;
        var retained = versions.Value.Count(v => v.Status == MapVersionStatus.Retained);
        var scope = request.Reader.Caller.Kind == ContextCallerKind.Session ? request.Reader.Caller.Repo : null;
        return new MapStatusView(version, MapCounts.Of(shown, layer.Count), orphaned, retained, scope);
    }
}
