using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>
/// Applies the override layer over a map version (spec §6.1), oldest first. Pure: the stored version never changes.
/// An undone override and the undo rows act as if absent. A 'propose' is pending until a person accepts or rejects it, and an
/// undo only cancels another row, so neither is ever orphaned (S-4).
/// </summary>
public static partial class OverrideApplier
{
    /// <summary>Applies <paramref name="overrides"/> to <paramref name="graph"/>.</summary>
    /// <param name="graph">The map version as built or imported.</param>
    /// <param name="overrides">Every override.</param>
    /// <returns>The graph a reader sees, and the orphaned overrides.</returns>
    public static AppliedMap Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides) => Apply(graph, overrides, copied: null);

    /// <summary>Applies <paramref name="overrides"/>, calling <paramref name="copied"/> once per copy of the graph made (the size test counts them).</summary>
    internal static AppliedMap Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, Action? copied)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        var orphaned = new List<MapOverride>();
        var inapplicable = new List<MapOverride>();
        var working = new Working(graph, copied);
        foreach (var o in OverrideLog.Active(overrides))
        {
            if (!working.Apply(o))
            {
                (working.IsOrphan(o) ? orphaned : inapplicable).Add(o);
            }
        }

        return new AppliedMap(working.Snapshot(), orphaned, inapplicable);
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

    /// <summary>A node an add may create: the file validator's surface-kind rule; decisions come only from 'decisions:'.</summary>
    private static bool Addable(NodeKey key) => NodeKinds.HasValidSurfaceKind(key) && key.Kind != NodeKind.Decision;

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
