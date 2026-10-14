using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowNode;

/// <summary>One node, its aliases and its edges, from the map the caller may read.</summary>
internal sealed class ShowNodeService : IService<ShowNodeRequest, NodeView>
{
    public ValueTask<Result<NodeView>> Handle(ShowNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
