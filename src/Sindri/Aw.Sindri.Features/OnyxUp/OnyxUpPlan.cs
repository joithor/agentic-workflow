using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// The steps of `onyx up` in the one order they run, checked when the service is composed: exactly fetch, config, pull, start, each naming the one before it as its upstream.
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

        for (var i = 0; i < list.Count; i++)
        {
            var expected = i == 0 ? null : list[i - 1].Name;
            if (!string.Equals(list[i].After, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"the {list[i].Name} step builds its stamp from {list[i].After ?? "nothing"}, not from the step before it ({expected ?? "none"})");
            }
        }

        return list;
    }
}
