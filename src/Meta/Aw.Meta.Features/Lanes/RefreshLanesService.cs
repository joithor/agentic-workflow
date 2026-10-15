using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Lanes;

internal sealed class RefreshLanesService : IService<RefreshLanesRequest, LanesResponse>
{
    public ValueTask<Result<LanesResponse>> Handle(RefreshLanesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
