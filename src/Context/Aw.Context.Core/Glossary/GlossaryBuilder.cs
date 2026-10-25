using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>Builds a <see cref="MapGlossary"/> from feature vectors: pure and deterministic, no I/O.</summary>
internal static class GlossaryBuilder
{
    internal static MapGlossary Build(IReadOnlyList<FeatureVectors> features, GlossaryRules rules)
    {
        var ordered = features.OrderBy(f => f.Feature.Value, StringComparer.Ordinal).ToList();
        var used = ordered.Take(rules.MaxFeatures).ToList();
        var vectored = new List<(FeatureVectors Feature, Centroid Centroid)>();
        var unvectored = new List<NodeKey>();
        foreach (var feature in used)
        {
            var unit = GlossaryCentroids.Unit(feature);
            if (unit is null)
            {
                unvectored.Add(feature.Feature);
                continue;
            }

            vectored.Add((feature, new Centroid(feature.Feature, unit)));
        }

        var groups = GlossaryClustering.Group([.. vectored.Select(v => v.Centroid)], rules.Similarity);
        var members = groups.Select(g => (IReadOnlyList<FeatureVectors>)[.. g.Select(i => vectored[i].Feature)]).ToList();
        var ranked = GlossaryTermRanking.Rank(members, rules);
        var clusters = members.Select((m, i) => new GlossaryCluster([.. m.Select(f => f.Feature)], [.. ranked[i].Select(t => t.Term)])).ToList();
        return new MapGlossary(MergeTerms(clusters, ranked), clusters, unvectored, Truncated: ordered.Count > rules.MaxFeatures);
    }

    // A term in several clusters takes the features of all of them and its highest weight.
    private static List<GlossaryTerm> MergeTerms(List<GlossaryCluster> clusters, List<List<(string Term, double Weight)>> ranked)
    {
        var byTerm = new Dictionary<string, (double Weight, SortedSet<string> Features)>(StringComparer.Ordinal);
        for (var i = 0; i < clusters.Count; i++)
        {
            foreach (var (term, weight) in ranked[i])
            {
                var entry = byTerm.TryGetValue(term, out var existing) ? existing : (0.0, new SortedSet<string>(StringComparer.Ordinal));
                entry.Features.UnionWith(clusters[i].Features.Select(f => f.Value));
                byTerm[term] = (Math.Max(entry.Item1, weight), entry.Features);
            }
        }

        return [.. byTerm
            .OrderByDescending(t => t.Value.Weight)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => new GlossaryTerm(t.Key, t.Value.Weight, [.. t.Value.Features.Select(NodeKey.From)]))];
    }
}
