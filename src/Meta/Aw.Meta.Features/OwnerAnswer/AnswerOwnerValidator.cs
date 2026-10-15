using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.OwnerAnswer;

internal sealed class AnswerOwnerValidator : IValidator<AnswerOwnerRequest>
{
    public Result<Unit> Validate(AnswerOwnerRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Question < 1 || string.IsNullOrWhiteSpace(value.Answer)
            ? MetaErrors.Invalid.With("an answer needs a question number from 1 and some text")
            : Unit.Value;
    }
}
