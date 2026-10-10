using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetBrief;

internal sealed class GetBriefService : IService<GetBriefRequest, GetBriefResponse>
{
    public ValueTask<Result<GetBriefResponse>> Handle(GetBriefRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetBrief: make GetBriefTests pass");
}
