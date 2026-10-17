namespace Aw.Context.Core.Results;

/// <summary>A node a changed file implements.</summary>
/// <param name="Key">The node's key (a node key such as 'feature:layout.split-pane').</param>
/// <param name="Confirmed">True when a confirmed 'implements' edge reaches it from this file; false when only a suggested one does.</param>
public sealed record DiffNode(string Key, bool Confirmed);
