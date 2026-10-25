using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetLabelStatus;

internal sealed class GetLabelStatusService : IService<GetLabelStatusRequest, GetLabelStatusResponse>
{
    public ValueTask<Result<GetLabelStatusResponse>> Handle(GetLabelStatusRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetLabelStatus: make GetLabelStatusTests pass (cutover unit 6.3)");
}
