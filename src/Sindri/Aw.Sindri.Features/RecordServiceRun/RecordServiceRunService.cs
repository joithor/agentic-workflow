using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RecordServiceRun;

internal sealed class RecordServiceRunService : IService<RecordServiceRunRequest, RecordServiceRunResponse>
{
    public ValueTask<Result<RecordServiceRunResponse>> Handle(RecordServiceRunRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RecordServiceRun: P2 unit C makes the scheduler's service.ran tests pass");
}
