using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Init;

internal sealed class InitValidator : IValidator<InitRequest>
{
    public Result<Unit> Validate(InitRequest value) => Unit.Value;
}
