using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OwnerAsk;

internal sealed class AskOwnerValidator : IValidator<AskOwnerRequest>
{
    public Result<Unit> Validate(AskOwnerRequest value) => throw new NotSupportedException();
}
