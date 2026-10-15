using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.GateRerun;

internal sealed class GateRerunValidator : IValidator<GateRerunRequest>
{
    public Result<Unit> Validate(GateRerunRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Pr < 1 || string.IsNullOrWhiteSpace(value.Step) || string.IsNullOrWhiteSpace(value.Test)
            ? MetaErrors.Invalid.With("a pull request number, a step name and a test are required")
            : Unit.Value;
    }
}
