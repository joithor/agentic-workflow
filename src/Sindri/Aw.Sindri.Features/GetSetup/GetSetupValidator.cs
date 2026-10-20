using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetSetup;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class GetSetupValidator : IValidator<GetSetupRequest>
{
    public Result<Unit> Validate(GetSetupRequest value) => Unit.Value;
}
