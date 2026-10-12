using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ExportMap;

/// <summary>Writes the current version (with its overrides) to the exports folder.</summary>
internal sealed class ExportMapService : IService<ExportMapRequest, MapExported>
{
    public ValueTask<Result<MapExported>> Handle(ExportMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
