using Aw.BugFix.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordBuildFailedService : IService<RecordBuildFailedRequest, RecordRunResponse>
{
    public ValueTask<Result<RecordRunResponse>> Handle(RecordBuildFailedRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("record-run --build-failed is not implemented yet");
}
