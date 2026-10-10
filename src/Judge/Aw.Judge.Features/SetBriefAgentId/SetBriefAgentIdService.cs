using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetBriefAgentId;

internal sealed class SetBriefAgentIdService : IService<SetBriefAgentIdRequest, SetBriefAgentIdResponse>
{
    public ValueTask<Result<SetBriefAgentIdResponse>> Handle(SetBriefAgentIdRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SetBriefAgentId: make SetBriefAgentIdTests pass");
}
