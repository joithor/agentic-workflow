using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GetContextMap (S-3a).</summary>
internal sealed class GetContextMapService : IService<GetContextMapRequest, MapStatusView>
{
    public ValueTask<Result<MapStatusView>> Handle(GetContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
