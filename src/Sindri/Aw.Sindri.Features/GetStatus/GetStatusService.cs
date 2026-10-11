using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetStatus;

internal sealed class GetStatusService : IService<GetStatusRequest, GetStatusResponse>
{
    public ValueTask<Result<GetStatusResponse>> Handle(GetStatusRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetStatus: make GetStatusTests pass");
}
