using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>ImportContextMap (S-3a).</summary>
internal sealed class ImportContextMapService : IService<ImportContextMapRequest, ImportContextMapResponse>
{
    public ValueTask<Result<ImportContextMapResponse>> Handle(ImportContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
