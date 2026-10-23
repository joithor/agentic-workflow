using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.WholeMap;

/// <summary>Builds the whole-map answer from a graph and its areas (Part D, §2.1). Pure apart from the text policy it writes labels with.</summary>
public static class WholeMapBuilder
{
    private const string OverBudget = "over-budget";

    /// <summary>The answer for one graph.</summary>
    /// <param name="ask">The map version, the detail and the keys to place.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="areas">Its areas.</param>
    /// <param name="rules">The limits.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view.</returns>
    public static WholeMapView Build(WholeMapAsk ask, GraphSnapshot graph, AreaIndex areas, WholeMapRules rules, MapText text)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(text);
        var known = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => known.ContainsKey(e.From.Value) && known.ContainsKey(e.To.Value)).ToList();
        var over = known.Count > rules.NodeBudget;
        var level = ask.Detail == WholeMapDetail.Areas || over ? WholeMapDetail.Areas : WholeMapDetail.Nodes;
        var (listed, truncated) = ListAreas(known, areas, rules, text);
        var kept = listed.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var basis = ask.Basis;
        return new WholeMapView(
            1, basis.MapVersion, basis.MapCommit, basis.Draft, basis.AsOf, basis.Repo, level,
            level == WholeMapDetail.Areas && ask.Detail != WholeMapDetail.Areas ? OverBudget : null,
            Counts(known.Values, edges.Count), listed, AreaEdges(edges, areas, kept), level == WholeMapDetail.Nodes ? Nodes(known.Values, areas, text) : null,
            level == WholeMapDetail.Nodes ? Edges(edges) : null, Area: null, Located(ask.Locate, areas, text), truncated);
    }

    private static string KindName(NodeKind kind) => kind.ToString().ToLowerInvariant();

    private static WholeMapCounts Counts(IEnumerable<GraphNode> nodes, int edges)
    {
        var list = nodes.ToList();
        return new WholeMapCounts(list.Count, edges, Enum.GetValues<NodeKind>().ToDictionary(KindName, k => list.Count(n => n.Key.Kind == k), StringComparer.Ordinal));
    }

    private static (IReadOnlyList<WholeMapArea> Areas, bool Truncated) ListAreas(Dictionary<string, GraphNode> known, AreaIndex areas, WholeMapRules rules, MapText text)
    {
        List<WholeMapArea> all = [.. known.Values.GroupBy(n => areas.ByNode[n.Key.Value].Area, StringComparer.Ordinal).Select(g => Area(g.Key, [.. g], known, text))];
        var truncated = all.Count > rules.AreasCap;
        var chosen = truncated ? all.OrderByDescending(a => a.Members).ThenBy(a => a.Key, StringComparer.Ordinal).Take(rules.AreasCap) : all;
        return ([.. chosen.OrderBy(a => a.Key, StringComparer.Ordinal)], truncated);
    }

    private static WholeMapArea Area(string key, List<GraphNode> members, Dictionary<string, GraphNode> known, MapText text)
    {
        var kinds = Enum.GetValues<NodeKind>().Select(k => (Name: KindName(k), Count: members.Count(n => n.Key.Kind == k))).Where(k => k.Count > 0).ToDictionary(k => k.Name, k => k.Count, StringComparer.Ordinal);
        if (key == AreaIndex.UnassignedKey || !known.TryGetValue(key, out var root))
        {
            return new WholeMapArea(key, AreaIndex.UnassignedLabel, Results.Trust.Trusted, members.Count, kinds, key == AreaIndex.UnassignedKey);
        }

        var trust = TrustPolicy.ForNode(root, Confirmation.Of(root.Provenance));
        return new WholeMapArea(key, text.Label(root.Label, trust), trust, members.Count, kinds, Unassigned: false);
    }

    private static List<WholeMapAreaEdge> AreaEdges(List<GraphEdge> edges, AreaIndex areas, HashSet<string> kept) =>
        [.. edges
            .Select(e => (From: areas.ByNode[e.From.Value].Area, To: areas.ByNode[e.To.Value].Area, e.Type, Confirmed: Confirmation.Of(e.Provenance)))
            .Where(e => e.From != e.To && kept.Contains(e.From) && kept.Contains(e.To))
            .GroupBy(e => (e.From, e.To, e.Type))
            .Select(g => new WholeMapAreaEdge(g.Key.From, g.Key.To, g.Key.Type, g.Count(), g.Count(e => e.Confirmed)))
            .OrderBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal).ThenBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal)];

    private static List<WholeMapNode> Nodes(IEnumerable<GraphNode> nodes, AreaIndex areas, MapText text) =>
        [.. nodes
            .Select(n => (Node: n, Area: areas.ByNode[n.Key.Value]))
            .OrderBy(p => p.Area.Area, StringComparer.Ordinal).ThenBy(p => p.Node.Key.Kind).ThenBy(p => p.Node.Key.Value, StringComparer.Ordinal)
            .Select(p => Node(p.Node, p.Area, text))];

    private static WholeMapNode Node(GraphNode node, NodeArea area, MapText text)
    {
        var confirmed = Confirmation.Of(node.Provenance);
        var trust = TrustPolicy.ForNode(node, confirmed);
        return new WholeMapNode(node.Key, text.Label(node.Label, trust), node.Key.Kind, area.Area, area.AlsoIn, trust, node.Provenance, node.Confidence, confirmed);
    }

    private static List<WholeMapEdge> Edges(List<GraphEdge> edges) =>
        [.. edges
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal)
            .Select(e => new WholeMapEdge(e.From, e.To, e.Type, LinkIds.For(e), e.Provenance, e.Confidence, Confirmation.Of(e.Provenance)))];

    private static List<WholeMapLocated>? Located(IReadOnlyList<string> keys, AreaIndex areas, MapText text) =>
        keys.Count == 0 ? null : [.. keys.Select(k => new WholeMapLocated(text.Line(k), areas.ByNode.TryGetValue(k, out var found) ? found.Area : null))];
}
