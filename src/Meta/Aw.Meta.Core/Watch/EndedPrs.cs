using System.Globalization;
namespace Aw.Meta.Core.Watch;

/// <summary>The pull requests that ended on GitHub (merged or closed), kept by the watch as a comma separated list.</summary>
public static class EndedPrs
{
    /// <summary>Reads the stored list.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <returns>The numbers.</returns>
    public static IReadOnlySet<int> Parse(string? stored) => Read(stored);

    private static HashSet<int> Read(string? stored) =>
        stored is null
            ? []
            : [.. stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static n => int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var pr) ? pr : 0)
                .Where(static pr => pr > 0)];

    /// <summary>The stored list with the ended pull requests of one poll added.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="prs">The pull requests the poll saw.</param>
    /// <returns>The new list, or null when nothing was added.</returns>
    public static string? Merge(string? stored, IEnumerable<PrView> prs)
    {
        var known = Read(stored);
        var before = known.Count;
        known.UnionWith(prs.Where(static pr => pr.State != PrState.Open).Select(static pr => pr.Number));
        return known.Count == before ? null : string.Join(',', known.Order().Select(static n => n.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The pull requests of one poll that ended and were not in the stored list yet.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="prs">The pull requests the poll saw.</param>
    /// <returns>Their numbers, ascending.</returns>
    public static IReadOnlyList<int> Added(string? stored, IEnumerable<PrView> prs)
    {
        ArgumentNullException.ThrowIfNull(prs);
        var known = Read(stored);
        return [.. prs.Where(pr => pr.State != PrState.Open && !known.Contains(pr.Number)).Select(static pr => pr.Number).Distinct().Order()];
    }
}
