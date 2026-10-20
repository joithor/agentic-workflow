using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.MapHeads;

/// <summary>Stub (red).</summary>
internal sealed class MapHeadsService(IRepoReader repos, IDefaultBranch defaults) : IService<MapHeadsRequest, IReadOnlyList<RepoHead>>
{
    public ValueTask<Result<IReadOnlyList<RepoHead>>> Handle(MapHeadsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"F1: not built yet {repos} {defaults}");
}
