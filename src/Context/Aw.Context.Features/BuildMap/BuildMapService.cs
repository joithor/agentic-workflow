using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.BuildMap;

/// <summary>Proposes the map from the repo and writes a draft (heavy-job lock, extraction, clusters, optional labeling). (S-4 stub: red until its slice is built.)</summary>
internal sealed class BuildMapService : IService<BuildMapRequest, MapBuildReport>
{
    public ValueTask<Result<MapBuildReport>> Handle(BuildMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: BuildMap is not built yet");
}
