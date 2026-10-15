using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.StopCheck;

internal sealed class StopCheckService : IService<StopCheckRequest, StopCheckResponse>
{
    public ValueTask<Result<StopCheckResponse>> Handle(StopCheckRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
