using Aw.Context.Core;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The map-quality slot over nothing (the query tests never measure).</summary>
internal sealed class NoMapQuality : IService<MapQualityRequest, MapQualityRun>
{
    public ValueTask<Result<MapQualityRun>> Handle(MapQualityRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<MapQualityRun>>(ContextErrors.NoMap.With("this test world has no map"));
}
