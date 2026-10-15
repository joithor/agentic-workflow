using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.CheckMerge;

internal sealed class CheckMergeService : IService<CheckMergeRequest, CheckMergeResponse>
{
    public ValueTask<Result<CheckMergeResponse>> Handle(CheckMergeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
