namespace Aw.Context.Core.Graph;

/// <summary>
/// The cascade (spec §6.1): what a change to an anchor affects, computed per query over typed steps. Own scope is free;
/// a state hop (writes a thing another node reads), a dependency hop (reverse depends-on) and a flow hop (the next step
/// of a shared flow) cost 1 each; a reached surface or action is reported as its owning feature (ascend, free), and only
/// the reached node steps on, never the ascended feature's own scope. Breadth first under a depth cap with a visited
/// set, so each node is reported once with its shortest path. Pure.
/// </summary>
public static class Cascade
{
    /// <summary>The anchor's own scope and the nodes up to <paramref name="depth"/> steps from it.</summary>
    /// <param name="graph">The graph a caller may see (overrides applied, a session's scope already cut).</param>
    /// <param name="anchor">A node key in <paramref name="graph"/> (resolve aliases first).</param>
    /// <param name="depth">The depth cap, 0 to 4 (0: own scope only).</param>
    /// <returns>The own scope and the affected nodes.</returns>
    public static CascadeResult Affects(GraphSnapshot graph, NodeKey anchor, int depth)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var index = new GraphIndex(graph);
        var own = OwnScope(index, anchor);
        var ownKeys = own.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(ownKeys, StringComparer.Ordinal);
        var affected = new List<AffectedNode>();
        var reported = new HashSet<string>(ownKeys, StringComparer.Ordinal);
        var frontier = own.Select(k => new Reach(k, [anchor.Value], [])).ToList();
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            var next = new List<Reach>();
            foreach (var reach in frontier)
            {
                foreach (var step in Steps(index, reach.Node).Where(s => visited.Add(s.Node.Value)))
                {
                    var reached = new Reach(step.Node, [.. reach.Path, .. step.Path], [.. reach.Edges, .. step.Edges]);
                    next.Add(reached);
                    var owner = Ascend(index, step.Node);
                    if (reported.Add(owner.Value))
                    {
                        affected.Add(new AffectedNode(owner, level, reached.Path, reached.Edges));
                    }
                }
            }

            frontier = [.. next.OrderBy(r => r.Node.Value, StringComparer.Ordinal)];
        }

        return new CascadeResult(own, [.. affected.OrderBy(a => a.Depth).ThenBy(a => a.Node.Value, StringComparer.Ordinal)]);
    }

    /// <summary>The anchor first, then by key: a feature's descendant features, their surfaces and those surfaces' actions; a surface's actions.</summary>
    private static List<NodeKey> OwnScope(GraphIndex index, NodeKey anchor)
    {
        var features = new List<NodeKey>();
        if (anchor.Kind == NodeKind.Feature)
        {
            var queue = new Queue<NodeKey>([anchor]);
            var seen = new HashSet<string>(StringComparer.Ordinal) { anchor.Value };
            while (queue.TryDequeue(out var feature))
            {
                features.Add(feature);
                foreach (var child in index.In(feature, EdgeType.PartOf).Select(e => e.From).Where(k => k.Kind == NodeKind.Feature && seen.Add(k.Value)))
                {
                    queue.Enqueue(child);
                }
            }
        }

        var surfaces = anchor.Kind == NodeKind.Surface
            ? [anchor]
            : features.SelectMany(f => index.In(f, EdgeType.PartOf)).Select(e => e.From).Where(k => k.Kind == NodeKind.Surface).ToList();
        var actions = surfaces.SelectMany(s => index.In(s, EdgeType.On)).Select(e => e.From);
        var rest = features.Skip(1).Concat(surfaces).Concat(actions)
            .Where(k => k.Value != anchor.Value).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal);
        return [anchor, .. rest];
    }

    /// <summary>The steps from one node: a feature hops to its dependents; a surface or action makes state and flow hops; a thing is read by its readers.</summary>
    private static IEnumerable<Step> Steps(GraphIndex index, NodeKey node) => node.Kind switch
    {
        NodeKind.Feature => index.In(node, EdgeType.DependsOn).Select(e => new Step(e.From, [$"depended-on-by {e.From.Value}"], [e])),
        NodeKind.Surface or NodeKind.Action => StateHops(index, node).Concat(FlowHops(index, node)),
        NodeKind.Thing => Readers(index, node, prefix: null),
        NodeKind.Flow or NodeKind.Decision or NodeKind.Record or NodeKind.Code => [],
        _ => [],
    };

    private static IEnumerable<Step> StateHops(GraphIndex index, NodeKey node) =>
        index.Out(node, EdgeType.Writes).SelectMany(write => Readers(index, write.To, write).Where(s => s.Node.Value != node.Value));

    private static IEnumerable<Step> Readers(GraphIndex index, NodeKey thing, GraphEdge? prefix) =>
        index.In(thing, EdgeType.Reads).Select(read => prefix is null
            ? new Step(read.From, [$"reads {read.From.Value}"], [read])
            : new Step(read.From, [$"writes {thing.Value}", $"reads {read.From.Value}"], [prefix, read]));

    private static IEnumerable<Step> FlowHops(GraphIndex index, NodeKey node) =>
        index.Out(node, EdgeType.StepOf).Where(e => e.Order is not null).SelectMany(own =>
            index.In(own.To, EdgeType.StepOf).Where(e => e.Order > own.Order).GroupBy(e => e.Order).OrderBy(g => g.Key).Take(1).SelectMany(g => g)
                .Select(next => new Step(next.From, [$"step-of {own.To.Value}", $"next {next.From.Value}"], [own, next])));

    /// <summary>A surface's owning feature (part-of), an action's through its surface (on, then part-of); the node itself when it has none.</summary>
    private static NodeKey Ascend(GraphIndex index, NodeKey node)
    {
        var surface = node.Kind == NodeKind.Action ? index.Out(node, EdgeType.On).Select(e => (NodeKey?)e.To).FirstOrDefault() : node;
        if (surface is not { Kind: NodeKind.Surface } s)
        {
            return node;
        }

        return index.Out(s, EdgeType.PartOf).Select(e => (NodeKey?)e.To).FirstOrDefault(k => k is { Kind: NodeKind.Feature }) ?? node;
    }

    private sealed record Reach(NodeKey Node, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges);

    private sealed record Step(NodeKey Node, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges);
}
