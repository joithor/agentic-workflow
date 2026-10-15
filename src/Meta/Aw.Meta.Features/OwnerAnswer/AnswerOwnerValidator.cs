using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OwnerAnswer;

internal sealed class AnswerOwnerValidator : IValidator<AnswerOwnerRequest>
{
    public Result<Unit> Validate(AnswerOwnerRequest value) => throw new NotSupportedException();
}
