using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.MergeRun;

internal sealed class MergeRunService : IService<MergeRunRequest, MergeRunResponse>
{
    public ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
