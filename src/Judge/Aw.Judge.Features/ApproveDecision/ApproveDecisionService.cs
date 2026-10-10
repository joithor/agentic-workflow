using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ApproveDecision;

internal sealed class ApproveDecisionService : IService<ApproveDecisionRequest, ApproveDecisionResponse>
{
    public ValueTask<Result<ApproveDecisionResponse>> Handle(ApproveDecisionRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ApproveDecision: make ApproveDecisionTests pass");
}
