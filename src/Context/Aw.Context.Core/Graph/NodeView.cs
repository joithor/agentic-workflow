namespace Aw.Context.Core.Graph;

/// <summary>'context show &lt;key&gt;' (spec §8.5): one node, its aliases and its edges.</summary>
/// <param name="MapVersion">The map version read ('v1', 'v12@a41b07d').</param>
/// <param name="Node">The node, with where it came from and a description.</param>
/// <param name="Aliases">The other keys that name it.</param>
/// <param name="Out">Edges leaving it, by target key, each with the target's kind and label.</param>
/// <param name="In">Edges reaching it, by source key, each with the source's kind and label.</param>
public sealed record NodeView(string MapVersion, NodeDetail Node, IReadOnlyList<NodeKey> Aliases, IReadOnlyList<EdgeDetail> Out, IReadOnlyList<EdgeDetail> In);
