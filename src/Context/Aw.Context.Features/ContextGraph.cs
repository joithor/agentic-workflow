using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.ShowNode;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The graph reads of the facade and the app's lenses and file-map reads: each method dispatches to its slice (spec §4).</summary>
internal sealed class ContextGraph(
    IService<ShowNodeRequest, NodeView> show,
    IService<ListNeighborsRequest, NeighborsView> neighbors,
    IService<FindPathRequest, PathView> path,
    IService<GetLensesRequest, LensesView> lenses,
    IService<GetLensesSetRequest, LensesSetView> lensesSet,
    IService<GetFileMapRequest, FileMapView> fileMap) : IContextGraph, IContextLenses
{
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) =>
        show.Handle(new ShowNodeRequest(key, scope), cancellationToken);

    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        neighbors.Handle(new ListNeighborsRequest(key, depth, scope), cancellationToken);

    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) =>
        path.Handle(new FindPathRequest(from, to, scope), cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        Lenses(anchor, read, new LensRange(depth), scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        lenses.Handle(new GetLensesRequest(anchor, read, range.Depth, scope) { Hops = range.Hops }, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        LensesOf(anchors, read, new LensRange(depth), scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        lensesSet.Handle(new GetLensesSetRequest(anchors, read, range.Depth, scope) { Hops = range.Hops }, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, bool draft, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        Lenses(anchor, draft ? MapRead.NewestDraft : MapRead.Current, depth, scope, cancellationToken);

    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        fileMap.Handle(new GetFileMapRequest(query, scope), cancellationToken);
}
