using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OutboxRecord;

internal sealed class OutboxRecordService : IService<OutboxRecordRequest, OutboxRecordResponse>
{
    public ValueTask<Result<OutboxRecordResponse>> Handle(OutboxRecordRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
