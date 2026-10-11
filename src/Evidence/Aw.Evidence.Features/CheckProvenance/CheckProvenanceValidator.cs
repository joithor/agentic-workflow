using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.CheckProvenance;

internal sealed class CheckProvenanceValidator : IValidator<CheckProvenanceRequest>
{
    public Result<Unit> Validate(CheckProvenanceRequest value) => Unit.Value;
}
