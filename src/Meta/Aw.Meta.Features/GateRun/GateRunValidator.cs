using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.GateRun;

internal sealed class GateRunValidator : IValidator<GateRunRequest>
{
    public Result<Unit> Validate(GateRunRequest value) => throw new NotSupportedException();
}
