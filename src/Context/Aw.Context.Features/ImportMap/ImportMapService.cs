using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ImportMap;

/// <summary>Imports a map file as the current version, bound to the content hash the owner confirmed.</summary>
internal sealed class ImportMapService : IService<ImportMapRequest, MapImported>
{
    public ValueTask<Result<MapImported>> Handle(ImportMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
