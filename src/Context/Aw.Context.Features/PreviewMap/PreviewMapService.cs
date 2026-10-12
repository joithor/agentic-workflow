using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PreviewMap;

/// <summary>Reads and checks a map file without writing: what the owner confirms by its hash.</summary>
internal sealed class PreviewMapService : IService<PreviewMapRequest, MapFilePreview>
{
    public ValueTask<Result<MapFilePreview>> Handle(PreviewMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
