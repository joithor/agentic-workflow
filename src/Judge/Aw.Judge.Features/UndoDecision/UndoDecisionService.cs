using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.UndoDecision;

internal sealed class UndoDecisionService : IService<UndoDecisionRequest, UndoDecisionResponse>
{
    public ValueTask<Result<UndoDecisionResponse>> Handle(UndoDecisionRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("UndoDecision: make UndoDecisionTests pass");
}
