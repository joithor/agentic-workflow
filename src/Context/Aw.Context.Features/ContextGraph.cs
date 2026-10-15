using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.ShowNode;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The graph reads of the facade: each method dispatches to its slice (spec §4).</summary>
internal sealed class ContextGraph(
    IService<ShowNodeRequest, NodeView> show,
    IService<ListNeighborsRequest, NeighborsView> neighbors,
    IService<FindPathRequest, PathView> path) : IContextGraph
{
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) =>
        show.Handle(new ShowNodeRequest(key, scope), cancellationToken);

    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        neighbors.Handle(new ListNeighborsRequest(key, depth, scope), cancellationToken);

    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) =>
        path.Handle(new FindPathRequest(from, to, scope), cancellationToken);
}
