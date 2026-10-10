using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordRunService : IService<RecordRunRequest, RecordRunResponse>
{
    public ValueTask<Result<RecordRunResponse>> Handle(RecordRunRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RecordRun: make RecordRunTests pass");
}
