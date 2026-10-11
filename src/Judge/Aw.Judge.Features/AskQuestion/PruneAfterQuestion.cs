using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

// Stub: the retention decorator around AskQuestion.
internal sealed class PruneAfterQuestion(IService<AskQuestionRequest, AskQuestionResponse> inner, IDecisionLog decisions, TimeProvider clock)
    : Decorated<AskQuestionRequest, AskQuestionResponse>(inner)
{
    public override ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        _ = (decisions, clock);
        return Inner.Handle(request, cancellationToken);
    }
}
