using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CreateItem;

internal sealed class CreateItemValidator : IValidator<CreateItemRequest>
{
    public Result<Unit> Validate(CreateItemRequest value) => Unit.Value;
}
