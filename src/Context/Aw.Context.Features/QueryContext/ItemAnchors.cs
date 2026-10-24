using Aw.Context.Core.Graph;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The item shape's anchors (spec §8.1, S-5B): the record a tracker key or record key names in the map the caller may read, the nodes it mentions, and the
/// nodes its decisions decide. A record is in the map only when a sync linked it, so a session sees only records linked to nodes in its repo.
/// </summary>
internal static class ItemAnchors
{
    private const int MaxSuggestions = 25;   // what to ask for instead: a list a person reads in a glance, not a result set

    internal static GraphNode? Record(GraphSnapshot graph, string item)
    {
        var text = item.Trim();
        string[] candidates = text.StartsWith("record:", StringComparison.Ordinal) ? [text] : ["record:" + text, text];
        return candidates.Select(c => NodeKey.TryFrom(c, out var key) ? graph.Resolve(key) : null).FirstOrDefault(n => n is { Key.Kind: NodeKind.Record });
    }

    /// <summary>The record first, then the nodes it mentions and its decisions decide, in key order.</summary>
    internal static IReadOnlyList<GraphNode> Touched(GraphSnapshot graph, GraphNode record)
    {
        var decisions = graph.Edges.Where(e => e.Type == EdgeType.Quotes && e.To.Value == record.Key.Value).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        var touched = graph.Edges.Where(e => (e.Type == EdgeType.Mentions && e.From.Value == record.Key.Value) || (e.Type == EdgeType.Decides && decisions.Contains(e.From.Value)))
            .Select(e => graph.Resolve(e.To)).OfType<GraphNode>().Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision))
            .DistinctBy(n => n.Key.Value, StringComparer.Ordinal).OrderBy(n => n.Key.Value, StringComparer.Ordinal);
        return [record, .. touched];
    }

    /// <summary>Tracker keys of records the map holds that share the item's team prefix (what to ask for instead).</summary>
    internal static IReadOnlyList<string> Suggest(GraphSnapshot graph, string item)
    {
        var dash = item.IndexOf('-', StringComparison.Ordinal);
        var team = dash > 0 ? item[..(dash + 1)] : item;
        return [.. graph.Aliases.Where(a => a.Alias.Kind == NodeKind.Record).Select(a => a.Alias.Name)
            .Where(name => name.StartsWith(team, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(MaxSuggestions)];
    }
}
