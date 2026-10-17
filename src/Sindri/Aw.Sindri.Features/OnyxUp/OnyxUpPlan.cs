using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// The steps of `onyx up` in the one order they run, checked when the service is composed: exactly fetch, config, pull, start. The service chains the stamps in this order: each step is given the stamp of the one before it.
internal static class OnyxUpPlan
{
    internal static IReadOnlyList<IOnyxUpStep> Validate(IEnumerable<IOnyxUpStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        List<IOnyxUpStep> list = [.. steps];
        if (!list.Select(static s => s.Name).SequenceEqual(OnyxSteps.Order, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"the steps of `onyx up` must be {string.Join(", ", OnyxSteps.Order)} in that order, not {string.Join(", ", list.Select(static s => s.Name))}");
        }

        return list;
    }
}
