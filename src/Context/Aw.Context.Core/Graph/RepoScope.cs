namespace Aw.Context.Core.Graph;

/// <summary>
/// The nodes a repo implements (spec §3): the targets of 'implements' edges with file evidence in that repo
/// ('repo:&lt;repo&gt;/&lt;path&gt;'), and the code files those edges start from ("that repo's files and code"). S-3b adds their cascade.
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
}
