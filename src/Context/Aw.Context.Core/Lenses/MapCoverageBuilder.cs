using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Counts how well the map covers the files behind a scope (scoping spec C13), from the same rollup 'context filemap' uses so the two never disagree.</summary>
internal static class MapCoverageBuilder
{
    /// <summary>The coverage of the files that implement <paramref name="own"/> nodes.</summary>
    internal static MapCoverage Of(GraphSnapshot graph, IReadOnlySet<string> own, LensBasis basis) =>
        throw new NotSupportedException("red");
}
