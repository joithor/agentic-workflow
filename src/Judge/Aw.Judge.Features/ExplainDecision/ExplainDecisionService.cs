using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainDecision;

internal sealed class ExplainDecisionService : IService<ExplainDecisionRequest, ExplainDecisionResponse>
{
    public ValueTask<Result<ExplainDecisionResponse>> Handle(ExplainDecisionRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ExplainDecision: make ExplainDecisionTests pass");
}
