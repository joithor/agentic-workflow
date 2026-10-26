using Aw.Context.Core.ConceptScan;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Regroups the area groups with the scan's concept hints, most confident first. A hinted group keeps the id of the area group whose seed it holds, else of the one it holds more than half the members of
/// (an area's id goes to one hint only); else it gets the first of its members that no group uses as an id. What a hint leaves of an area that lost its id or its seed becomes a group of its own, under its
/// first member no group uses. A member the groups lack is ignored, and a feature stays in the first hint that names it.
/// </summary>
internal static class ConceptHintGroups
{
    internal static Dictionary<string, List<string>> Apply(Dictionary<string, List<string>> areas, IReadOnlyList<ConceptHint> hints)
    {
        var known = areas.Values.SelectMany(static m => m).ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(areas.Keys, StringComparer.Ordinal);
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var hint in hints.OrderByDescending(static h => h.Confidence).ThenBy(static h => h.Name, StringComparer.Ordinal))
        {
            var members = hint.Members.Distinct(StringComparer.Ordinal).Where(m => known.Contains(m) && !taken.Contains(m)).Order(StringComparer.Ordinal).ToList();
            var key = members.Count == 0 ? null : Kept(areas, members, claimed) ?? members.Find(m => !used.Contains(m));
            if (key is not null)
            {
                _ = used.Add(key);
                taken.UnionWith(members);
                result[key] = members;
            }
        }

        foreach (var (seed, all) in areas.OrderBy(static a => a.Key, StringComparer.Ordinal))
        {
            Leftover(result, used, seed, [.. all.Where(m => !taken.Contains(m))], seedStays: !taken.Contains(seed));
        }

        return result;
    }

    // The area whose id a hinted group keeps: the one whose seed it holds, else the one it holds more than half the members of (the most of them first).
    private static string? Kept(Dictionary<string, List<string>> areas, List<string> members, HashSet<string> claimed)
    {
        var held = members.ToHashSet(StringComparer.Ordinal);
        var key = areas.Keys.Order(StringComparer.Ordinal).FirstOrDefault(k => !claimed.Contains(k) && held.Contains(k))
            ?? areas.Where(a => !claimed.Contains(a.Key) && a.Value.Count(held.Contains) * 2 > a.Value.Count)
                .OrderByDescending(a => a.Value.Count(held.Contains)).ThenBy(static a => a.Key, StringComparer.Ordinal).Select(static a => a.Key).FirstOrDefault();
        if (key is not null)
        {
            _ = claimed.Add(key);
        }

        return key;
    }

    private static void Leftover(Dictionary<string, List<string>> result, HashSet<string> used, string seed, List<string> rest, bool seedStays)
    {
        if (rest.Count == 0)
        {
            return;
        }

        if (seedStays && !result.ContainsKey(seed))
        {
            result[seed] = rest;
            return;
        }

        var key = rest.Find(m => !used.Contains(m) && !result.ContainsKey(m));
        if (key is not null)
        {
            _ = used.Add(key);
            result[key] = rest;
        }
        else if (result.TryGetValue(seed, out var group))
        {
            group.AddRange(rest);   // only seeds are left of it: they follow the group that kept the id
            group.Sort(StringComparer.Ordinal);
        }
        else
        {
            result[seed] = rest;
        }
    }
}
