using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Watch;

internal sealed class WatchService : IService<WatchRequest, WatchResponse>
{
    public ValueTask<Result<WatchResponse>> Handle(WatchRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
