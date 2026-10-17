using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the file map of a directory (spec §8.9, S-4). Pure over an applied graph and a file listing.</summary>
public static class FileMapBuilder
{
    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map version and the commit listed.</param>
    /// <param name="query">What was asked.</param>
    /// <param name="files">Every tracked file of the repo, paths from the root.</param>
    /// <param name="graph">The graph a caller may see: overrides applied.</param>
    /// <returns>The view, or SND-CONTEXT-003 when no file lies under the path.</returns>
    public static Result<FileMapView> Build(FileMapBasis basis, FileMapQuery query, IReadOnlyList<string> files, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(graph);
        var normal = RepoPaths.Normalize(query.Path);
        var path = normal.Length == 0 ? normal : normal + "/";
        var under = files.Where(f => f.StartsWith(path, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (path.Length > 0 && under.Count == 0)
        {
            return ContextErrors.UnknownAnchor.With($"no file lies under '{(path.Length <= 80 ? path : "(a long path)")}' in {query.Repo.Value} (fix: list the parent directory)");
        }

        var tree = new FileLinks(graph);
        var entries = FileMapRollup.Entries(path, under, tree);
        var suggested = under.SelectMany(tree.Suggestions).ToList();
        var totals = Totals(under, tree);
        return new FileMapView(1, query.Repo, basis.Sha, basis.MapVersion, query.Draft, path, [.. query.UnmappedOnly ? entries.Where(e => e.Status != FileMapStatus.Mapped) : entries],
            [.. entries.Where(e => e.Status == FileMapStatus.Unmapped).Select(e => new FileMapUnmapped(e.Path, e.Files))], suggested, totals, LensSymbols.Unavailable);
    }

    /// <summary>Counts by status over <paramref name="under"/>: the one rollup 'context filemap' and a lens answer's map coverage share.</summary>
    internal static FileMapTotals Totals(IReadOnlyList<string> under, FileLinks tree) =>
        new(under.Count, under.Count(f => tree.StatusOf(f) == FileMapStatus.Mapped), under.Count(f => tree.StatusOf(f) == FileMapStatus.Suggested),
            under.Count(f => tree.StatusOf(f) == FileMapStatus.Unmapped));
}
