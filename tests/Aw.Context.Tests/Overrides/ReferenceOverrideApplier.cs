using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Overrides;

/// <summary>The applier as it was before it applied in place (each override copied the graph): the reference the in-place one must match exactly. Test-only.
/// Applies the override layer over a map version (spec §6.1), oldest first. Pure: the stored version never changes.
/// An undone override and the undo rows act as if absent. A 'propose' is pending until a person accepts or rejects it, and an
/// undo only cancels another row, so neither is ever orphaned (S-4).
/// </summary>
internal static class ReferenceOverrideApplier
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
        foreach (var o in OverrideLog.Active(overrides))
        {
            var next = ApplyOne(graph, o);
            if (next is not null)
            {
                graph = next;
            }
            else if (IsOrphan(graph, o))
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

    // A link decision is orphaned when its edge is gone; a node decision when its target no longer resolves. A reject or delete of something gone is satisfied, not orphaned.
    private static bool IsOrphan(GraphSnapshot graph, MapOverride o) => o.Action switch
    {
        OverrideAction.Add or OverrideAction.Propose or OverrideAction.Undo => false,
        OverrideAction.Reject or OverrideAction.Delete => false,   // satisfied once its target is gone (a deny cut took it out); the row stays, so the item stays rejected if it returns
        _ when o.Link is { } link => !graph.Edges.Any(e => LinkIds.For(e).Value == link.Value),
        OverrideAction.Accept or OverrideAction.Reject or OverrideAction.Rename or OverrideAction.Merge or OverrideAction.Move or OverrideAction.Delete => graph.Resolve(o.Target) is null,
        _ => false,
    };

    private static GraphSnapshot? ApplyOne(GraphSnapshot graph, MapOverride o)
    {
        if (o.Action == OverrideAction.Add)
        {
            return Add(graph, o);
        }

        if (o.Action == OverrideAction.Propose)
        {
            return Propose(graph, o);
        }

        if (o.Link is { } link)
        {
            return DecideLink(graph, o, link);
        }

        var target = graph.Resolve(o.Target);
        if (target is null)
        {
            return null;
        }

        return o.Action switch
        {
            OverrideAction.Accept => Accept(Replace(graph, target, target with { Provenance = Provenance.Human }), target.Key),
            OverrideAction.Rename when !string.IsNullOrWhiteSpace(o.Label) => Replace(graph, target, target with { Label = o.Label }),
            OverrideAction.Reject => o.Rejects(RejectionIdentity.For(target)) ? Remove(graph, target.Key) : graph,
            OverrideAction.Delete => Remove(graph, target.Key),
            OverrideAction.Move or OverrideAction.Merge => WithOther(graph, target, o),
            OverrideAction.Rename or OverrideAction.Add or OverrideAction.Propose or OverrideAction.Undo => null,   // add and propose are handled above; undo rows never reach here

            _ => null,
        };
    }

    // A proposal's own parent edge is confirmed with it.
    private static GraphSnapshot Accept(GraphSnapshot graph, NodeKey key) => graph with
    {
        Edges = [.. graph.Edges.Select(e => e is { Type: EdgeType.PartOf, Provenance: Provenance.Model } && e.From.Value == key.Value ? e with { Provenance = Provenance.Human } : e)],
    };

    private static GraphSnapshot? DecideLink(GraphSnapshot graph, MapOverride o, LinkId link)
    {
        var edge = graph.Edges.FirstOrDefault(e => LinkIds.For(e).Value == link.Value);
        return (o.Action, edge) switch
        {
            (_, null) => null,
            (OverrideAction.Accept, _) => graph with { Edges = [.. graph.Edges.Select(e => ReferenceEquals(e, edge) ? e with { Provenance = Provenance.Human } : e)] },
            (OverrideAction.Reject, _) => graph with { Edges = [.. graph.Edges.Where(e => !ReferenceEquals(e, edge))] },
            _ => null,
        };
    }

    private static GraphSnapshot? Propose(GraphSnapshot graph, MapOverride o)
    {
        if (graph.Resolve(o.Target) is not null)
        {
            return graph;
        }

        if (ProposalRows.NodeOf(o) is not { } node)
        {
            return null;
        }

        var parent = o.Proposal?.Parent is { } key ? graph.Resolve(key)?.Key : null;
        GraphEdge[] edges = parent is { } p && EdgeRules.Allows(EdgeType.PartOf, o.Target.Kind, p.Kind)
            ? [new GraphEdge(o.Target, p, EdgeType.PartOf, Provenance.Model, node.Confidence, node.Evidence, At: o.At)]
            : [];
        return graph with { Nodes = [.. graph.Nodes, node], Edges = [.. graph.Edges, .. edges] };
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

        if (!Addable(o.Target))
        {
            return null;
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

    /// <summary>A node an add may create: the file validator's surface-kind rule; decisions come only from 'decisions:'.</summary>
    private static bool Addable(NodeKey key) => NodeKinds.HasValidSurfaceKind(key) && key.Kind != NodeKind.Decision;

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
