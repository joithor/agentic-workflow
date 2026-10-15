using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>
/// What the repos' deny globs take out of a map (context spec §3, "Denied paths"), judged once on the graph as a caller reads it (overrides applied) and then
/// applied to any form of it: the code nodes of denied paths, the nodes whose only evidence is a denied file, the features only those files implemented,
/// every edge touching them and the overrides about them. Denied evidence is struck from what stays.
/// </summary>
internal sealed class DenyCut
{
    private readonly IReadOnlyList<ContextRepo> _repos;
    private readonly HashSet<string> _removed;
    private readonly HashSet<string> _cutLinks;

    private DenyCut(IReadOnlyList<ContextRepo> repos, HashSet<string> removed, HashSet<string> cutLinks, bool any)
    {
        _repos = repos;
        _removed = removed;
        _cutLinks = cutLinks;
        Any = any;
    }

    /// <summary>Whether the globs take or strike anything from the graph this was judged on.</summary>
    public bool Any { get; }

    /// <summary>Judges the applied graph.</summary>
    /// <param name="stored">The graph as stored, before overrides: a feature the model made stays one the model made after a person accepts it.</param>
    /// <param name="applied">The graph with the overrides applied.</param>
    /// <param name="repos">The repos whose deny globs apply.</param>
    /// <returns>The cut.</returns>
    public static DenyCut Of(GraphSnapshot stored, GraphSnapshot applied, IReadOnlyList<ContextRepo> repos)
    {
        var removed = applied.Nodes.Where(n => IsDeniedCode(n.Key, repos) || (n.Provenance != Provenance.Human && OnlyDenied(n.EvidenceOrEmpty, repos))).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var cut = applied.Edges.Where(e => removed.Contains(e.From.Value) || removed.Contains(e.To.Value) || IsEvidenceCut(e, repos)).ToList();
        removed.UnionWith(OrphanedFeatures(stored, applied, cut, removed));
        var cutLinks = applied.Edges.Where(e => removed.Contains(e.From.Value) || removed.Contains(e.To.Value) || IsEvidenceCut(e, repos)).Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var any = removed.Count > 0 || cutLinks.Count > 0 || applied.Nodes.Any(n => HasDenied(n.EvidenceOrEmpty, repos)) || applied.Edges.Any(e => HasDenied(e.EvidenceOrEmpty, repos));
        return new DenyCut(repos, removed, cutLinks, any);
    }

    /// <summary>The graph without what was cut: the nodes, the edges (by their endpoints or link), and denied evidence struck from the rest.</summary>
    /// <param name="graph">The graph, as stored or with overrides applied.</param>
    /// <returns>The graph itself when nothing is denied.</returns>
    public GraphSnapshot Apply(GraphSnapshot graph)
    {
        if (!Any)
        {
            return graph;
        }

        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => !_removed.Contains(n.Key.Value)).Select(n => n with { Evidence = Allowed(n.Evidence, _repos) })],
            Edges = [.. graph.Edges.Where(e => !_removed.Contains(e.From.Value) && !_removed.Contains(e.To.Value) && !_cutLinks.Contains(LinkIds.For(e).Value)).Select(e => e with { Evidence = Allowed(e.Evidence, _repos) })],
        };
    }

    /// <summary>The overrides that still mean something: not about a removed node, a cut link or a denied code path, and a proposal without its denied evidence (or at all when that was all it had).</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The overrides a caller may see.</returns>
    public IReadOnlyList<MapOverride> Apply(IReadOnlyList<MapOverride> overrides)
    {
        var kept = new List<MapOverride>(overrides.Count);
        foreach (var o in overrides)
        {
            if (About(o))
            {
                continue;
            }

            if (o.Proposal is { Evidence: { Count: > 0 } evidence } payload && HasDenied(evidence, _repos))
            {
                if (OnlyDenied(evidence, _repos))
                {
                    continue;
                }

                kept.Add(o with { Proposal = payload with { Evidence = Allowed(evidence, _repos) } });
                continue;
            }

            kept.Add(o);
        }

        return kept;
    }

    private bool About(MapOverride o) =>
        _removed.Contains(o.Target.Value) || IsDeniedCode(o.Target, _repos) || (o.Other is { } other && (_removed.Contains(other.Value) || IsDeniedCode(other, _repos)))
        || (o.Link is { } link && _cutLinks.Contains(link.Value))
        || (o.Proposal is { Evidence: { Count: > 0 } evidence } && OnlyDenied(evidence, _repos));

    private static bool IsDeniedCode(NodeKey key, IReadOnlyList<ContextRepo> repos) =>
        key.Kind == NodeKind.Code && repos.Any(r => r.Denies(key.Value["code:".Length..]));

    private static bool IsEvidenceCut(GraphEdge e, IReadOnlyList<ContextRepo> repos) => e.Provenance != Provenance.Human && OnlyDenied(e.EvidenceOrEmpty, repos);

    // The model-made features left with no implementing file once the cut edges are gone, accepted or not: a person accepting a model feature doesn't give its denied file back.
    // A feature a person wrote (human in the stored graph) stays.
    private static HashSet<string> OrphanedFeatures(GraphSnapshot stored, GraphSnapshot graph, List<GraphEdge> cut, HashSet<string> removed)
    {
        var madeByModel = stored.Nodes.Where(n => n.Provenance == Provenance.Model).Select(n => n.Key.Value).Concat(graph.Nodes.Where(n => n.Provenance == Provenance.Model).Select(n => n.Key.Value)).ToHashSet(StringComparer.Ordinal);
        var cutSet = cut.ToHashSet();
        var stillImplemented = graph.Edges.Where(e => e.Type == EdgeType.Implements && !cutSet.Contains(e)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        var features = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature && madeByModel.Contains(n.Key.Value)).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return cut.Where(e => e.Type == EdgeType.Implements).Select(e => e.To.Value)
            .Where(feature => features.Contains(feature) && !removed.Contains(feature) && !stillImplemented.Contains(feature)).ToHashSet(StringComparer.Ordinal);
    }

    private static bool Denies(EvidenceRef reference, IReadOnlyList<ContextRepo> repos)
    {
        var key = reference.Key.Value;
        return repos.Any(r => key.StartsWith($"repo:{r.Name.Value}/", StringComparison.Ordinal) && r.Denies(key[$"repo:{r.Name.Value}/".Length..]));
    }

    private static bool HasDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Any(r => Denies(r, repos));

    private static bool OnlyDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Count > 0 && evidence.All(r => Denies(r, repos));

    private static IReadOnlyList<EvidenceRef>? Allowed(IReadOnlyList<EvidenceRef>? evidence, IReadOnlyList<ContextRepo> repos) =>
        evidence is null || !HasDenied(evidence, repos) ? evidence : [.. evidence.Where(r => !Denies(r, repos))];
}
