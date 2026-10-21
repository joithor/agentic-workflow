using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReleaseMap;

/// <summary>Removes the hold on a draft; the draft and the wait's start stay.</summary>
internal sealed class ReleaseMapService(IGraphStore store) : IService<ReleaseMapRequest, Unit>
{
    public ValueTask<Result<Unit>> Handle(ReleaseMapRequest request, CancellationToken cancellationToken) =>
        PublishAccess.Gate(request.Scope, "releasing a hold") is { } refused
            ? ValueTask.FromResult<Result<Unit>>(refused)
            : store.Release(request.Version, cancellationToken);
}
