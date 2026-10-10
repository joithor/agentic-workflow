using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByName;

internal sealed class MapBriefByNameService : IService<MapBriefByNameRequest, MapBriefByNameResponse>
{
    public ValueTask<Result<MapBriefByNameResponse>> Handle(MapBriefByNameRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("MapBriefByName: make MapBriefByNameTests pass");
}
