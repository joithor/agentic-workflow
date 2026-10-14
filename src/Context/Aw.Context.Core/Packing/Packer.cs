namespace Aw.Context.Core.Packing;

/// <summary>
/// Packs items into the budget (spec §8.3): sections in fill order (graph, affects, decisions, code, records); within
/// a section, greedily in the given order (best first), each item whole or not at all; an item that doesn't fit is
/// listed by its marker only. Conflicts and degraded sources are outside the budget and never pass through here.
/// </summary>
public static class Packer
{
    /// <summary>Packs <paramref name="candidates"/> into <paramref name="budget"/> tokens.</summary>
    /// <param name="candidates">The items, best first within each section.</param>
    /// <param name="budget">The token budget.</param>
    /// <returns>The sections, the tokens used and whether anything was cut to a reference.</returns>
    public static Packed Pack(IReadOnlyList<PackCandidate> candidates, int budget)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var used = 0;
        var truncated = false;
        var sections = new List<Results.ResultSection>();
        foreach (var name in Results.ResultSection.FillOrder)
        {
            var items = new List<Results.ResultItem>();
            var more = new List<string>();
            foreach (var candidate in candidates.Where(c => string.Equals(c.Section, name, StringComparison.Ordinal)))
            {
                if (used + candidate.Item.Tokens <= budget)
                {
                    items.Add(candidate.Item);
                    used += candidate.Item.Tokens;
                }
                else
                {
                    more.Add(candidate.Marker);
                    truncated = true;
                }
            }

            if (items.Count > 0 || more.Count > 0)
            {
                sections.Add(new Results.ResultSection(name, items, more));
            }
        }

        return new Packed(sections, used, truncated);
    }
}
