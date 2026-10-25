using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Derives the concepts of a map (concepts note §2): groups of features from the part-of roots, merged to at most 15, pins first, names from a model or a deterministic fallback.</summary>
public static class ConceptDeriver
{
    /// <summary>The derived groups, for the namer: pinned concepts are not among them.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <returns>The groups in seed order.</returns>
    public static IReadOnlyList<ConceptGroup> Groups(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins) => throw new NotSupportedException();

    /// <summary>The concept nodes and the belongs-to edges of a map.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="names">What a model named the groups, or null for the fallback.</param>
    /// <returns>The concepts, 'concept:other' among them when the map has a feature, and the membership edges.</returns>
    public static ExtractedMap Derive(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, ConceptNames? names) => throw new NotSupportedException();
}
