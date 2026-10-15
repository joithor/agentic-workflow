using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.GateRerun;

internal sealed class GateRerunValidator : IValidator<GateRerunRequest>
{
    public Result<Unit> Validate(GateRerunRequest value) => throw new NotSupportedException();
}
