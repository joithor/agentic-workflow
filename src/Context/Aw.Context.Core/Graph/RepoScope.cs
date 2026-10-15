namespace Aw.Context.Core.Graph;

/// <summary>
/// The nodes a repo implements (spec §3): the targets of 'implements' edges with file evidence in that repo
/// ('repo:&lt;repo&gt;/&lt;path&gt;'), and the code files those edges start from ("that repo's files and code"). S-3b adds their cascade.
/// Since owner decision 2026-10-15 (B) a session also sees the intent linked to those nodes (<see cref="WithLinkedIntent"/>), without waiting for 'context.repoTeams'.
/// </summary>
public static class RepoScope
{
    /// <summary>The keys of the nodes <paramref name="repo"/> implements.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="repo">The repo's short name.</param>
    /// <returns>The node keys (ordinal), empty when it implements nothing.</returns>
    public static IReadOnlySet<string> NodesFor(GraphSnapshot graph, Engine.ContextRepoName repo)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var prefix = $"repo:{repo.Value}/";
        return graph.Edges
            .Where(e => e.Type == EdgeType.Implements && e.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(e => new[] { e.To.Value, e.From.Value })
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// <paramref name="scope"/> and the intent linked to it (spec §3 "intent records linked to those nodes"; owner decision 2026-10-15, B): the decisions with a 'decides' edge to a
    /// node in <paramref name="scope"/>, the records with a 'mentions' edge to one, and the records those decisions quote. A record or decision linked only to nodes outside
    /// <paramref name="scope"/> is not in the result. Nothing is added to an empty scope.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="scope">The nodes the repo implements (<see cref="NodesFor"/>).</param>
    /// <returns>The keys of the scope and of its linked records and decisions (ordinal).</returns>
    public static IReadOnlySet<string> WithLinkedIntent(GraphSnapshot graph, IReadOnlySet<string> scope)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0)
        {
            return scope;
        }

        var decisions = graph.Edges.Where(e => e.Type == EdgeType.Decides && e.From.Kind == NodeKind.Decision && scope.Contains(e.To.Value)).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        var records = graph.Edges.Where(e => e.Type == EdgeType.Mentions && e.From.Kind == NodeKind.Record && scope.Contains(e.To.Value)).Select(e => e.From.Value)
            .Concat(graph.Edges.Where(e => e.Type == EdgeType.Quotes && e.To.Kind == NodeKind.Record && decisions.Contains(e.From.Value)).Select(e => e.To.Value));
        var all = new HashSet<string>(scope, StringComparer.Ordinal);
        all.UnionWith(decisions);
        all.UnionWith(records);
        return all;
    }
}
