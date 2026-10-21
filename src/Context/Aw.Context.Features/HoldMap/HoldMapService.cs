using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.HoldMap;

/// <summary>Stores the hold next to the latest draft; the store reads the wait's start from the chain row in the same transaction (retrieval spec §8.7a).</summary>
internal sealed class HoldMapService(IGraphStore store) : IService<HoldMapRequest, MapHold>
{
    public ValueTask<Result<MapHold>> Handle(HoldMapRequest request, CancellationToken cancellationToken) =>
        PublishAccess.Gate(request.Scope, "holding a draft") is { } refused
            ? ValueTask.FromResult<Result<MapHold>>(refused)
            : store.Hold(request.Hold, cancellationToken);
}
