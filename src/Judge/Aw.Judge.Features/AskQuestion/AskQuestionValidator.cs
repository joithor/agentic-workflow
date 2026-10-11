using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

internal sealed class AskQuestionValidator : IValidator<AskQuestionRequest>
{
    public Result<Unit> Validate(AskQuestionRequest value) => Unit.Value;
}
