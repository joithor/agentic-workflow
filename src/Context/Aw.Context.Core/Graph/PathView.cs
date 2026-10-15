namespace Aw.Context.Core.Graph;

/// <summary>'context path &lt;a&gt; &lt;b&gt;' (spec §8.5): the shortest path of at most 8 edges, either way. No path is an answer, not an error.</summary>
/// <param name="MapVersion">The map version read.</param>
/// <param name="From">The start (an alias resolved).</param>
/// <param name="To">The end (an alias resolved).</param>
/// <param name="Found">Whether a path of at most 8 edges exists.</param>
/// <param name="Steps">Its steps (empty when not found, or when both ends are the same node).</param>
public sealed record PathView(string MapVersion, NodeKey From, NodeKey To, bool Found, IReadOnlyList<PathFact> Steps);
