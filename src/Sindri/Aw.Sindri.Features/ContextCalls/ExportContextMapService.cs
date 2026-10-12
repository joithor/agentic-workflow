using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>ExportContextMap (S-3a).</summary>
internal sealed class ExportContextMapService : IService<ExportContextMapRequest, MapExported>
{
    public ValueTask<Result<MapExported>> Handle(ExportContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
