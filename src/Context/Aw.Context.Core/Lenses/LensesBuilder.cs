using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the lenses view around one node (spec §8.8, S-4). Pure over an applied graph.</summary>
public static class LensesBuilder
{
    /// <summary>The cap on each band's items.</summary>
    public const int BandCap = 100;

    private static readonly NodeKind[] _product = [NodeKind.Feature, NodeKind.Surface, NodeKind.Action, NodeKind.Thing, NodeKind.Flow];
    private static readonly EdgeType[] _across = [EdgeType.Mentions, EdgeType.Decides, EdgeType.Quotes, EdgeType.Supersedes, EdgeType.Implements, EdgeType.Tests];

    /// <summary>Builds the view.</summary>
    /// <param name="map">The map read ('v13@7c3e1a2').</param>
    /// <param name="draft">True when the draft was read.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchor">The anchor (a key or an alias).</param>
    /// <param name="depth">How far the neighbors reach and the cascade runs, 0 to 4.</param>
    /// <returns>The view, or SND-CONTEXT-003 when the graph has no such node.</returns>
    public static Result<LensesView> Build(string map, bool draft, GraphSnapshot graph, NodeKey anchor, int depth)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Resolve(anchor) is not { } node)
        {
            return ContextErrors.UnknownAnchor.With($"no node '{(anchor.Value.Length <= 80 ? anchor.Value : "(a long key)")}' in this map (fix: sindri context map status)");
        }

        var clamped = Math.Clamp(depth, 0, GraphWalk.MaxDepth);
        var cascade = Cascade.Affects(graph, node.Key, clamped);
        var nodes = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        var scope = cascade.OwnScope.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        var product = Product(GraphWalk.Neighbors(graph, node.Key, clamped), cascade, nodes, scope);
        var intent = Intent(graph, nodes, scope);
        var code = Code(graph, nodes, scope);
        var seen = intent.Concat(product).Concat(code).Select(i => i.Key.Value).ToHashSet(StringComparer.Ordinal);
        return new LensesView(map, draft, node.Key, Band(intent), Band(product), Band(code), new LensSymbols("unavailable", "index not built"),
            Edges(graph, seen), Conflicts(graph, scope), Coverage(graph, nodes, cascade.OwnScope), Gaps(graph, cascade.OwnScope));
    }

    private static LensBand Band(List<LensItem> items) => new([.. items.Take(BandCap)], items.Count > BandCap);

    private static List<LensItem> Product(Neighborhood neighborhood, CascadeResult cascade, Dictionary<string, GraphNode> nodes, HashSet<string> scope)
    {
        var items = new List<LensItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(NodeKey key, LensRelation relation)
        {
            if (nodes.TryGetValue(key.Value, out var n) && seen.Add(key.Value))
            {
                items.Add(Item(n, relation, link: null));
            }
        }

        foreach (var key in cascade.OwnScope)
        {
            Add(key, LensRelation.Own);
        }

        foreach (var near in neighborhood.Nodes.Where(n => n.Depth > 0 && _product.Contains(n.Node.Key.Kind) && !scope.Contains(n.Node.Key.Value)))
        {
            Add(near.Node.Key, LensRelation.Neighbor);
        }

        foreach (var far in cascade.Affected)
        {
            Add(far.Node, LensRelation.Affects);
        }

        return items;
    }

    private static List<LensItem> Intent(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, HashSet<string> scope) =>
        [.. graph.Edges.Where(e => e.Type is EdgeType.Mentions or EdgeType.Decides && scope.Contains(e.To.Value))
            .Select(e => e.From).DistinctBy(k => k.Value, StringComparer.Ordinal).Where(k => nodes.ContainsKey(k.Value))
            .OrderBy(k => k.Value, StringComparer.Ordinal).Select(k => Item(nodes[k.Value], LensRelation.Own, link: null))];

    private static List<LensItem> Code(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, HashSet<string> scope) =>
        [.. graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tests && scope.Contains(e.To.Value) && nodes.ContainsKey(e.From.Value))
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            {
                var best = g.OrderByDescending(e => Confirmation.Of(e.Provenance)).ThenByDescending(e => e.Confidence).ThenBy(e => e.To.Value, StringComparer.Ordinal).First();
                var file = nodes[g.Key];
                return new LensItem(file.Key, file.Label, LensRelation.Own, best.Provenance, best.Confidence, Confirmation.Of(best.Provenance), LinkIds.For(best));
            })];

    private static LensItem Item(GraphNode n, LensRelation relation, LinkId? link) =>
        new(n.Key, n.Label, relation, n.Provenance, n.Confidence, Confirmation.Of(n.Provenance), link);

    private static List<LensEdge> Edges(GraphSnapshot graph, HashSet<string> items) =>
        [.. graph.Edges.Where(e => _across.Contains(e.Type) && items.Contains(e.From.Value) && items.Contains(e.To.Value))
            .OrderBy(e => e.Type).ThenBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)
            .Select(e => new LensEdge(e.From, e.To, e.Type, LinkIds.For(e), Confirmation.Of(e.Provenance), e.Confidence))];

    private static List<LensConflict> Conflicts(GraphSnapshot graph, HashSet<string> scope)
    {
        var deciding = graph.Edges.Where(e => e.Type == EdgeType.Decides && scope.Contains(e.To.Value)).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        return [.. graph.Edges.Where(e => e.Type == EdgeType.Supersedes && deciding.Contains(e.From.Value))
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)
            .Select(e => new LensConflict("supersedes", e.From, e.To))];
    }

    private static LensCoverage Coverage(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, IReadOnlyList<NodeKey> scope)
    {
        var covered = Targets(graph, EdgeType.Mentions, EdgeType.Decides);
        var implemented = Targets(graph, EdgeType.Implements, EdgeType.Tests);
        return new LensCoverage(scope.Count, scope.Count(k => covered.Contains(k.Value)),
            scope.Count(k => nodes.TryGetValue(k.Value, out var n) && Confirmation.Of(n.Provenance)), scope.Count(k => implemented.Contains(k.Value)));
    }

    private static List<string> Gaps(GraphSnapshot graph, IReadOnlyList<NodeKey> scope)
    {
        var covered = Targets(graph, EdgeType.Mentions, EdgeType.Decides);
        var implemented = Targets(graph, EdgeType.Implements, EdgeType.Tests);
        return [.. scope.OrderBy(k => k.Value, StringComparer.Ordinal).Select(k => (Key: k, Missing: Missing(covered.Contains(k.Value), implemented.Contains(k.Value))))
            .Where(g => g.Missing.Length > 0).Select(g => $"{g.Key.Value}: {g.Missing}")];
    }

    private static string Missing(bool hasIntent, bool hasCode) => (hasIntent, hasCode) switch
    {
        (true, true) => string.Empty,
        (false, true) => "no intent",
        (true, false) => "no code",
        (false, false) => "no intent, no code",
    };

    private static HashSet<string> Targets(GraphSnapshot graph, EdgeType first, EdgeType second) =>
        graph.Edges.Where(e => e.Type == first || e.Type == second).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
}
