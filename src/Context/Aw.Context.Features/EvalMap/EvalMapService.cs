using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalMap;

/// <summary>Stub.</summary>
internal sealed class EvalMapService : IService<MapQualityRequest, MapQualityRun>
{
    public ValueTask<Result<MapQualityRun>> Handle(MapQualityRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
