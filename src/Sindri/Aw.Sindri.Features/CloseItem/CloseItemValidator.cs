using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CloseItem;

internal sealed class CloseItemValidator : IValidator<CloseItemRequest>
{
    public Result<Unit> Validate(CloseItemRequest value) => Unit.Value;
}
