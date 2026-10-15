using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Status;

internal sealed class StatusService : IService<StatusRequest, StatusResponse>
{
    public ValueTask<Result<StatusResponse>> Handle(StatusRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
