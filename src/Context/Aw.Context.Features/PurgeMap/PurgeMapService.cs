using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PurgeMap;

/// <summary>The context-purge service's work.</summary>
internal sealed class PurgeMapService : IService<PurgeMapRequest, PurgeReport>
{
    public ValueTask<Result<PurgeReport>> Handle(PurgeMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
