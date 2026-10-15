using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What an extraction found: nodes and edges, ready to join a draft.</summary>
/// <param name="Nodes">The nodes, by key.</param>
/// <param name="Edges">The edges, by ends and type.</param>
public sealed record ExtractedMap(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)
{
    /// <summary>Nothing found.</summary>
    public static ExtractedMap Empty { get; } = new([], []);

    /// <summary>Both extractions together: a node or an edge that both hold is kept once, the first one's.</summary>
    /// <param name="other">The other extraction.</param>
    /// <returns>The union, ordered by key.</returns>
    public ExtractedMap Plus(ExtractedMap other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _ = Nodes;
        throw new NotSupportedException("S-4: ExtractedMap.Plus is not built yet");
    }
}
