using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetConfig;

internal sealed class SetConfigValidator : IValidator<SetConfigRequest>
{
    public Result<Unit> Validate(SetConfigRequest value) => Unit.Value;
}
