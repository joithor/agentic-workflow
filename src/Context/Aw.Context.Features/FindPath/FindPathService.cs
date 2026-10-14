using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.FindPath;

/// <summary>The shortest path of at most 8 edges between two nodes, either way.</summary>
internal sealed class FindPathService : IService<FindPathRequest, PathView>
{
    public ValueTask<Result<PathView>> Handle(FindPathRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
