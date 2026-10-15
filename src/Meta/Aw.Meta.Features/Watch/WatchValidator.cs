using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.Watch;

internal sealed class WatchValidator : IValidator<WatchRequest>
{
    private static readonly TimeSpan _shortest = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _longest = TimeSpan.FromHours(24);

    public Result<Unit> Validate(WatchRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Interval < _shortest || value.Duration < value.Interval || value.Duration > _longest
            ? MetaErrors.Invalid.With("the interval must be at least 5 seconds and the duration between one interval and 24 hours")
            : Unit.Value;
    }
}
