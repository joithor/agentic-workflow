using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Counts how well a set of scope nodes is backed (spec §8.9). One function serves the lenses and 'context map status --anchor', so their numbers agree.</summary>
public static class LensCoverageBuilder
{
    /// <summary>The coverage of <paramref name="anchor"/>'s own scope.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="anchor">The anchor node.</param>
    /// <returns>The coverage.</returns>
    public static LensCoverage ForAnchor(GraphSnapshot graph, NodeKey anchor) => throw new NotSupportedException();
}
