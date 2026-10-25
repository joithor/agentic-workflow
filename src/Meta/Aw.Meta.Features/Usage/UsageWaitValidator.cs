using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.Usage;

internal sealed class UsageWaitValidator : IValidator<UsageWaitRequest>
{
    internal static readonly TimeSpan Shortest = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan Longest = TimeSpan.FromDays(8);

    public Result<Unit> Validate(UsageWaitRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Max < Shortest || value.Max > Longest
            ? MetaErrors.Invalid.With("--max must be between 1 minute and 8 days (192h)")
            : Unit.Value;
    }
}
