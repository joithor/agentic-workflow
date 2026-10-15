using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.GateRun;

internal sealed class GateRunValidator : IValidator<GateRunRequest>
{
    public Result<Unit> Validate(GateRunRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Pr < 1 ? MetaErrors.Invalid.With("a pull request number from 1 is required") : Unit.Value;
    }
}
