using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Readiness.Core;

namespace Aw.Readiness.App.Features.CheckRepo;

internal sealed class CheckRepoValidator : IValidator<CheckRepoRequest>
{
    public Result<Unit> Validate(CheckRepoRequest value) => string.IsNullOrWhiteSpace(value.Root)
        ? ReadinessErrors.Invalid.With("root must not be empty")
        : Unit.Value;
}
