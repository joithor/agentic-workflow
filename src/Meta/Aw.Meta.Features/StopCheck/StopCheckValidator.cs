using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.StopCheck;

internal sealed class StopCheckValidator : IValidator<StopCheckRequest>
{
    public Result<Unit> Validate(StopCheckRequest value) => throw new NotSupportedException();
}
