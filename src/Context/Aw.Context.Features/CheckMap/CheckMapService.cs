using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckMap;

/// <summary>Doctor 'context:map'.</summary>
internal sealed class CheckMapService : IService<CheckMapRequest, MapHealth>
{
    public ValueTask<Result<MapHealth>> Handle(CheckMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
