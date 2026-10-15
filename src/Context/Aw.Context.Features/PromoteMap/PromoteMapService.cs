using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PromoteMap;

/// <summary>Makes the latest draft current; refused while review items are undecided. (S-4 stub: red until its slice is built.)</summary>
internal sealed class PromoteMapService : IService<PromoteMapRequest, MapPromoted>
{
    public ValueTask<Result<MapPromoted>> Handle(PromoteMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: PromoteMap is not built yet");
}
