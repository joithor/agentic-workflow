namespace Aw.Context.Core.WholeMap;

/// <summary>One repo the whole map holds nodes of, for the page's repo picker.</summary>
/// <param name="Name">The repo's short name.</param>
/// <param name="NodeCount">How many nodes of the map belong to it (a node of no single repo counts for none).</param>
public sealed record WholeMapRepo(string Name, int NodeCount);
