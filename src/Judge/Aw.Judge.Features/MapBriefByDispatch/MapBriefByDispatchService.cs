using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByDispatch;

internal sealed class MapBriefByDispatchService : IService<MapBriefByDispatchRequest, MapBriefByDispatchResponse>
{
    public ValueTask<Result<MapBriefByDispatchResponse>> Handle(MapBriefByDispatchRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("MapBriefByDispatch: make MapBriefByDispatchTests pass");
}
