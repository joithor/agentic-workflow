namespace Aw.Context.Core.Graph;

/// <summary>Another key that resolves to a node: an older key after a rename or key migration, or a merged node's key.</summary>
/// <param name="Alias">The other key.</param>
/// <param name="Target">The node's key.</param>
public sealed record NodeAlias(NodeKey Alias, NodeKey Target);
