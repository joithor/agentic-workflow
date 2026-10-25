using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.EvaluateQuestion;

internal sealed class EvaluateQuestionService : IService<EvaluateQuestionRequest, EvaluateQuestionResponse>
{
    public ValueTask<Result<EvaluateQuestionResponse>> Handle(EvaluateQuestionRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("EvaluateQuestion: make EvaluateQuestionTests pass (cutover unit 6.3)");
}
