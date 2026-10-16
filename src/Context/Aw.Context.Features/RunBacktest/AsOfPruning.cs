using Aw.Context.Core.Graph;

namespace Aw.Context.Features.RunBacktest;

/// <summary>
/// Drops what the cut's tree doesn't have from an authored map (spec §9.1, "never uses a node that didn't exist at the cut"): a code node whose file is
/// absent, a node whose implementers are all absent, and, by the edges, a node that hangs only on dropped ones. A node with no edges at all stays.
/// </summary>
internal static class AsOfPruning
{
    internal static GraphSnapshot Existing(GraphSnapshot graph, HashSet<string> tree)
    {
        var implementers = graph.Edges.Where(e => e.Type == EdgeType.Implements).ToLookup(e => e.To.Value, e => e.From.Value, StringComparer.Ordinal);
        var alive = Implemented(graph, implementers, tree);
        var loose = graph.Nodes.Where(n => n.Key.Kind != NodeKind.Code && !implementers[n.Key.Value].Any()).Select(n => n.Key.Value).ToList();   // nothing implements these: they live by their edges
        Spread(graph, loose, alive);
        alive.UnionWith(loose.Where(k => !graph.Edges.Any(e => e.From.Value == k || e.To.Value == k)));
        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => alive.Contains(n.Key.Value))],
            Edges = [.. graph.Edges.Where(e => alive.Contains(e.From.Value) && alive.Contains(e.To.Value))],
            Aliases = [.. graph.Aliases.Where(a => alive.Contains(a.Target.Value))],
        };
    }

    // The code files the cut has, and the nodes one of them implements.
    private static HashSet<string> Implemented(GraphSnapshot graph, ILookup<string, string> implementers, HashSet<string> tree)
    {
        var alive = new HashSet<string>(graph.Nodes.Where(n => n.Key.Kind == NodeKind.Code && tree.Contains(PathOf(n.Key.Value))).Select(n => n.Key.Value), StringComparer.Ordinal);
        alive.UnionWith([.. graph.Nodes.Where(n => n.Key.Kind != NodeKind.Code && implementers[n.Key.Value].Any(alive.Contains)).Select(n => n.Key.Value)]);
        return alive;
    }

    // A loose node is alive when an edge joins it to an alive node; repeat until nothing more joins.
    private static void Spread(GraphSnapshot graph, List<string> loose, HashSet<string> alive)
    {
        bool changed;
        do
        {
            var joined = loose.Where(k => !alive.Contains(k) && graph.Edges.Any(e => Touches(e, k, alive))).ToList();
            changed = joined.Count > 0;
            alive.UnionWith(joined);
        }
        while (changed);
    }

    private static bool Touches(GraphEdge edge, string key, HashSet<string> alive) =>
        (edge.From.Value == key && alive.Contains(edge.To.Value)) || (edge.To.Value == key && alive.Contains(edge.From.Value));

    private static string PathOf(string codeKey)
    {
        var path = codeKey["code:".Length..];
        var hash = path.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? path : path[..hash];
    }
}
