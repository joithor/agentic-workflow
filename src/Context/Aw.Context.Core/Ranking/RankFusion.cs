using System.Globalization;

namespace Aw.Context.Core.Ranking;

/// <summary>
/// Fusion (spec §8.2 step 4): hits sharing a canonical key merge first, keeping each source's best rank (§5.2 dedup);
/// each source is then re-ranked by those best ranks, so a source with more sub-queries can't dominate; then
/// reciprocal rank fusion across sources, score = Σ 1 / (k + rank). Source scores never enter it, only ranks.
/// </summary>
public static class RankFusion
{
    /// <summary>The RRF constant.</summary>
    public const int K = 60;

    /// <summary>Fuses <paramref name="answers"/>.</summary>
    /// <param name="answers">Each source's hits.</param>
    /// <returns>One item per canonical key, best score first (ties by key, ordinal).</returns>
    public static IReadOnlyList<FusedItem> Fuse(IReadOnlyList<SourceAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var items = new Dictionary<string, (double Score, List<Sources.SourceHit> Hits, List<string> Why)>(StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            var best = answer.Hits
                .GroupBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal)
                .Select(g => g.MinBy(h => h.Rank)!)   // why: a group is never empty
                .OrderBy(h => h.Rank)
                .ThenBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal);
            var rank = 0;
            foreach (var hit in best)
            {
                rank++;
                var key = hit.Ref.Canonical.Value;
                var entry = items.TryGetValue(key, out var found) ? found : (0d, [], []);
                entry.Hits.Add(hit);
                entry.Why.Add(string.Create(CultureInfo.InvariantCulture, $"rrf:{answer.Source.Value}#{rank}"));
                items[key] = (entry.Item1 + (1d / (K + rank)), entry.Hits, entry.Why);
            }
        }

        return [.. items
            .Select(i => new FusedItem(i.Value.Hits[0].Ref.Canonical, i.Value.Score, i.Value.Hits, i.Value.Why))
            .OrderByDescending(i => i.Score)
            .ThenBy(i => i.Key.Value, StringComparer.Ordinal)];
    }
}
