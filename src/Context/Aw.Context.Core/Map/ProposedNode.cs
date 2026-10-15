using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>One node a build proposed.</summary>
/// <param name="Key">The node key.</param>
/// <param name="Provenance">Deterministic, or model for a cluster or label.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Files">The files that implement it ('code:' keys).</param>
public sealed record ProposedNode(NodeKey Key, Provenance Provenance, double Confidence, IReadOnlyList<NodeKey> Files);
