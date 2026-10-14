using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetQueue;

internal sealed class GetQueueService : IService<GetQueueRequest, QueueResponse>
{
    public ValueTask<Result<QueueResponse>> Handle(GetQueueRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetQueue: make GetQueueTests pass");
}
