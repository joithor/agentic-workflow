using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.HeldMap;

/// <summary>The publish state for the map service's tick (retrieval spec §8.7a): every repo's heads, so a session is refused (SND-CONTEXT-020); sessions read <c xml:lang="en">PublishStatus</c>.</summary>
internal sealed class HeldMapService(IGraphStore store) : IService<HeldMapRequest, MapPublishState>
{
    public ValueTask<Result<MapPublishState>> Handle(HeldMapRequest request, CancellationToken cancellationToken) =>
        PublishAccess.Gate(request.Scope, "reading the held map") is { } refused
            ? ValueTask.FromResult<Result<MapPublishState>>(refused)
            : store.Held(cancellationToken);
}
