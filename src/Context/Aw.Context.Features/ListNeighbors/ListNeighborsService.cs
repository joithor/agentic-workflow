using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ListNeighbors;

/// <summary>The nodes within some edges of one node, either way, and the edges among them.</summary>
internal sealed class ListNeighborsService : IService<ListNeighborsRequest, NeighborsView>
{
    public ValueTask<Result<NeighborsView>> Handle(ListNeighborsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
