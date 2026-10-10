using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.RunScript;

internal sealed class RunScriptValidator : IValidator<RunScriptRequest>
{
    public Result<Unit> Validate(RunScriptRequest value) => Unit.Value;
}
