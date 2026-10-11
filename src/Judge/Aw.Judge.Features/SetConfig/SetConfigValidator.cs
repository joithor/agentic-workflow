using Aw.Judge.Core.Errors;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetConfig;

internal sealed class SetConfigValidator : IValidator<SetConfigRequest>
{
    public Result<Unit> Validate(SetConfigRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Question.Length == 0)
        {
            return JudgeErrors.Invalid.With("the question name is empty");
        }

        return (value.Enabled is null) == (value.Threshold is null)
            ? JudgeErrors.Invalid.With("set exactly one of enabled and threshold")
            : Unit.Value;
    }
}
