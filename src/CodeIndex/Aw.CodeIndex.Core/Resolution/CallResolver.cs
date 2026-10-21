using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Resolution;

/// <summary>
/// Resolves raw syntax call sites to edges by name (spec §5.4), with no I/O: the repo's symbols and its manifest
/// directories in, ranked edges out.
/// </summary>
public sealed class CallResolver
{
    /// <summary>Creates a resolver over a repo's symbols.</summary>
    /// <param name="symbols">Every symbol of the repo.</param>
    /// <param name="projectDirectories">The directories that hold a manifest, repo-relative with '/' and no trailing slash; the root is the empty string. The nearest one is a file's project directory.</param>
    public CallResolver(IReadOnlyList<ResolutionSymbol> symbols, IReadOnlyCollection<string> projectDirectories) =>
        throw new NotSupportedException();

    /// <summary>Resolves call sites to edges.</summary>
    /// <param name="rows">The call sites with their callers, in source order.</param>
    /// <returns>The edges: one per call, or one per candidate (at most eight) for an ambiguous one, without duplicates.</returns>
    public IReadOnlyList<EdgeRecord> Resolve(IEnumerable<CallSiteRow> rows) => throw new NotSupportedException();

    /// <summary>Renders a candidate's place among the name's matches.</summary>
    /// <param name="rank">The 1-based rank.</param>
    /// <param name="candidates">The true number of matches.</param>
    /// <returns>For example '1 of 300'.</returns>
    public static string RankText(int rank, int candidates) => throw new NotSupportedException();
}
