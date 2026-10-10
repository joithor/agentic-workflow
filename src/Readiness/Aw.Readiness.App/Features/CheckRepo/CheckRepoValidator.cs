using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Readiness.App.Features.CheckRepo;

internal sealed class CheckRepoValidator : IValidator<CheckRepoRequest>
{
    public Result<Unit> Validate(CheckRepoRequest value) => Unit.Value;
}
