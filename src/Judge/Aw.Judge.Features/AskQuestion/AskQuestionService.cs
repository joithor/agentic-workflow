using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

internal sealed class AskQuestionService : IService<AskQuestionRequest, AskQuestionResponse>
{
    public ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: AskQuestion");
}
