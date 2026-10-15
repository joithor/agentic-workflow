using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Watch;

internal sealed class WatchValidator : IValidator<WatchRequest>
{
    public Result<Unit> Validate(WatchRequest value) => throw new NotSupportedException();
}
