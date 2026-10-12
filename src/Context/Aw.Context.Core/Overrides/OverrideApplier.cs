using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Overrides;

/// <summary>Applies the override layer over a map version (spec §6.1), oldest first. Pure: the stored version never changes.</summary>
public static class OverrideApplier
{
    /// <summary>Applies <paramref name="overrides"/> to <paramref name="graph"/>.</summary>
    /// <param name="graph">The map version as built or imported.</param>
    /// <param name="overrides">Every override.</param>
    /// <returns>The graph a reader sees, and the orphaned overrides.</returns>
    public static AppliedMap Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        var orphaned = new List<MapOverride>();
        var inapplicable = new List<MapOverride>();
        foreach (var o in overrides.Select(o => (Override: o, Id: o.ToId().Value)).OrderBy(x => x.Override.At).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Override))
        {
            var next = ApplyOne(graph, o);
            if (next is not null)
            {
                graph = next;
            }
            else if (o.Action != OverrideAction.Add && graph.Resolve(o.Target) is null)
            {
                orphaned.Add(o);
            }
            else
            {
                inapplicable.Add(o);
            }
        }

        return new AppliedMap(graph, orphaned, inapplicable);
    }

    private static GraphSnapshot? ApplyOne(GraphSnapshot graph, MapOverride o)
    {
        if (o.Action == OverrideAction.Add)
        {
            return Add(graph, o);
        }

        var target = graph.Resolve(o.Target);
        if (target is null)
        {
            return null;
        }

        return o.Action switch
        {
            OverrideAction.Accept => Replace(graph, target, target with { Provenance = Provenance.Human }),
            OverrideAction.Rename when !string.IsNullOrWhiteSpace(o.Label) => Replace(graph, target, target with { Label = o.Label }),
            OverrideAction.Reject => o.Rejection is null || o.Rejection == RejectionIdentity.For(target) ? Remove(graph, target.Key) : graph,
            OverrideAction.Delete => Remove(graph, target.Key),
            OverrideAction.Move or OverrideAction.Merge => WithOther(graph, target, o),
            OverrideAction.Rename or OverrideAction.Add => null,
            _ => null,
        };
    }

    private static GraphSnapshot? WithOther(GraphSnapshot graph, GraphNode target, MapOverride o)
    {
        var other = o.Other is { } key ? graph.Resolve(key) : null;
        var allowed = o.Action == OverrideAction.Move
            ? EdgeRules.Allows(EdgeType.PartOf, target.Key.Kind, other?.Key.Kind ?? NodeKind.Feature)
            : other?.Key.Kind == target.Key.Kind;
        if (other is null || other.Key.Value == target.Key.Value || !allowed)
        {
            return null;
        }

        return o.Action == OverrideAction.Move ? Move(graph, target.Key, other.Key) : Merge(graph, target.Key, other.Key);
    }

    private static GraphSnapshot? Add(GraphSnapshot graph, MapOverride o)
    {
        if (graph.Resolve(o.Target) is not null)
        {
            return graph;
        }

        var parent = o.Other is { } key ? graph.Resolve(key)?.Key : null;
        if (o.Other is not null && (parent is not { } p0 || !EdgeRules.Allows(EdgeType.PartOf, o.Target.Kind, p0.Kind)))
        {
            return null;
        }

        var node = new GraphNode(o.Target, o.Label ?? o.Target.Name, Provenance.Human, At: o.At);
        GraphEdge[] edges = parent is { } p ? [new GraphEdge(o.Target, p, EdgeType.PartOf, At: o.At)] : [];
        return graph with { Nodes = [.. graph.Nodes, node], Edges = [.. graph.Edges, .. edges] };
    }

    private static GraphSnapshot Replace(GraphSnapshot graph, GraphNode old, GraphNode next) =>
        graph with { Nodes = [.. graph.Nodes.Select(n => ReferenceEquals(n, old) ? next : n)] };

    private static GraphSnapshot Remove(GraphSnapshot graph, NodeKey key) => graph with
    {
        Nodes = [.. graph.Nodes.Where(n => n.Key.Value != key.Value)],
        Edges = [.. graph.Edges.Where(e => e.From.Value != key.Value && e.To.Value != key.Value)],
        Aliases = [.. graph.Aliases.Where(a => a.Target.Value != key.Value)],
    };

    private static GraphSnapshot Move(GraphSnapshot graph, NodeKey key, NodeKey parent) => graph with
    {
        Edges = [.. graph.Edges.Where(e => !(e.Type == EdgeType.PartOf && e.From.Value == key.Value)),
            new GraphEdge(key, parent, EdgeType.PartOf)],
    };

    private static GraphSnapshot Merge(GraphSnapshot graph, NodeKey from, NodeKey into)
    {
        NodeKey Map(NodeKey k) => k.Value == from.Value ? into : k;
        var edges = graph.Edges.Select(e => e with { From = Map(e.From), To = Map(e.To) })
            .Where(e => e.From.Value != e.To.Value)
            .DistinctBy(e => (e.From.Value, e.To.Value, e.Type));
        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => n.Key.Value != from.Value)],
            Edges = [.. edges],
            Aliases = [.. graph.Aliases.Select(a => a with { Target = Map(a.Target) }), new NodeAlias(from, into)],
        };
    }
}
