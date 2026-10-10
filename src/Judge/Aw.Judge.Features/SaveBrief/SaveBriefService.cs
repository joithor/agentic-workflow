using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SaveBrief;

internal sealed class SaveBriefService : IService<SaveBriefRequest, SaveBriefResponse>
{
    public ValueTask<Result<SaveBriefResponse>> Handle(SaveBriefRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SaveBrief: make SaveBriefTests pass");
}
