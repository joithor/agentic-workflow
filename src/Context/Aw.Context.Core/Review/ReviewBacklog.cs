using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Review;

/// <summary>Builds what is left to decide in a draft (spec §8.7, S-4): deterministic proposals, model proposals and suggested file links by impact, then orphaned overrides.</summary>
public static class ReviewBacklog
{
    private static readonly NodeKind[] _proposable = [NodeKind.Feature, NodeKind.Surface, NodeKind.Action, NodeKind.Thing, NodeKind.Flow];

    /// <summary>Builds the queue.</summary>
    /// <param name="label">The draft read ('v14@7c3e1a2').</param>
    /// <param name="draft">The draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <returns>The undecided entries and how many proposals already have a decision.</returns>
    public static ReviewQueueView Build(string label, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(overrides);
        var applied = OverrideApplier.Apply(draft, overrides);
        var graph = applied.Graph;
        var nodes = graph.Nodes.Where(n => n.Provenance != Provenance.Human && _proposable.Contains(n.Key.Kind)).Select(n => NodeEntry(graph, n));
        var links = graph.Edges.Where(e => e.Provenance == Provenance.Model && e.Type is EdgeType.Implements or EdgeType.Tests).Select(e => LinkEntry(graph, e));
        var undecided = nodes.Concat(links).ToList();
        var ordered = undecided.Where(e => e.Group == ReviewGroup.Deterministic).OrderBy(e => e.Node.Value, StringComparer.Ordinal)
            .Concat(undecided.Where(e => e.Group == ReviewGroup.Model).OrderByDescending(e => e.Impact).ThenBy(e => e.Node.Value, StringComparer.Ordinal).ThenBy(e => e.Link?.Value, StringComparer.Ordinal))
            .Concat(applied.Orphaned.Where(o => o.Action != OverrideAction.Propose).Select(Orphan));
        var decided = OverrideLog.Active(overrides).Where(OverrideLog.Decides).Select(o => o.Link?.Value ?? o.Target.Value).Distinct(StringComparer.Ordinal).Count();
        return new ReviewQueueView(label, [.. ordered], decided);
    }

    private static ReviewEntry NodeEntry(GraphSnapshot graph, GraphNode node)
    {
        var files = graph.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == node.Key.Value).Select(e => e.From).OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        var parent = graph.Edges.Where(e => e.Type == EdgeType.PartOf && e.From.Value == node.Key.Value).Select(e => (NodeKey?)e.To).FirstOrDefault();
        var impact = graph.Edges.Count(e => e.From.Value == node.Key.Value || e.To.Value == node.Key.Value);
        var group = node.Provenance == Provenance.Deterministic ? ReviewGroup.Deterministic : ReviewGroup.Model;
        return new ReviewEntry(node.Key, Link: null, group, node.Label, node.Provenance, node.Confidence, impact, files, parent);
    }

    private static ReviewEntry LinkEntry(GraphSnapshot graph, GraphEdge edge)
    {
        var to = graph.Resolve(edge.To)?.Label ?? edge.To.Name;
        return new ReviewEntry(edge.From, LinkIds.For(edge), ReviewGroup.Model, $"{edge.From.Name} {EdgeRules.Name(edge.Type)} {to}", edge.Provenance, edge.Confidence, 1, [edge.From], edge.To);
    }

    private static ReviewEntry Orphan(MapOverride o) =>
        new(o.Target, o.Link, ReviewGroup.Orphaned, $"{o.Action.ToString().ToLowerInvariant()} {o.Label ?? o.Target.Name}", Provenance.Human, 1, 0, [], Parent: null, o.ToId());
}
