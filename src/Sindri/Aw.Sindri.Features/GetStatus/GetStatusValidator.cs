using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetStatus;

internal sealed class GetStatusValidator : IValidator<GetStatusRequest>
{
    public Result<Unit> Validate(GetStatusRequest value) => Unit.Value;
}
