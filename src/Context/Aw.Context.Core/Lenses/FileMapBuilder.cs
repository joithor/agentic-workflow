using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the file map of a directory (spec §8.9, S-4). Pure over an applied graph and a file listing.</summary>
public static class FileMapBuilder
{
    /// <summary>The page size when the query pages (names a cursor or a limit) but gives no limit.</summary>
    public const int DefaultLimit = 200;

    /// <summary>The most entries one page holds.</summary>
    public const int MaxLimit = 1000;

    /// <summary>Builds the view from a listing; see <see cref="Build(FileMapBasis, FileMapQuery, RepoTree, GraphSnapshot)"/>.</summary>
    /// <param name="basis">The map version and the commit listed.</param>
    /// <param name="query">What was asked.</param>
    /// <param name="files">Every tracked file of the repo, paths from the root.</param>
    /// <param name="graph">The graph a caller may see: overrides applied.</param>
    /// <returns>The view, or SND-CONTEXT-003 when no file lies under the path.</returns>
    public static Result<FileMapView> Build(FileMapBasis basis, FileMapQuery query, IReadOnlyList<string> files, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(files);
        return Build(basis, query, new RepoTree(files), graph);
    }

    /// <summary>
    /// Builds one page of the view. The children are found without reading the files under them; the status, features and intent are read
    /// only for the entries on the page (all children when 'unmapped only' must judge each one), and the totals are one pass over the files under the path.
    /// </summary>
    /// <param name="basis">The map version and the commit listed.</param>
    /// <param name="query">What was asked; its cursor and limit pick the page.</param>
    /// <param name="tree">The repo's listing.</param>
    /// <param name="graph">The graph a caller may see: overrides applied.</param>
    /// <returns>The view, SND-CONTEXT-003 when no file lies under the path, or SND-CONTEXT-006 for a cursor that is not a child of it.</returns>
    public static Result<FileMapView> Build(FileMapBasis basis, FileMapQuery query, RepoTree tree, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(graph);
        var normal = RepoPaths.Normalize(query.Path);
        var path = normal.Length == 0 ? normal : normal + "/";
        var range = tree.Under(path);
        if (path.Length > 0 && range.End == range.Start)
        {
            return ContextErrors.UnknownAnchor.With($"no file lies under '{(path.Length <= 80 ? path : "(a long path)")}' in {query.Repo.Value} (fix: list the parent directory)");
        }

        var links = FileLinks.For(graph);
        var children = tree.Children(path, range);
        if (query.UnmappedOnly)
        {
            children = [.. children.Where(c => FileMapRollup.StatusOf(c, tree, links) != FileMapStatus.Mapped)];
        }

        var first = FirstAfter(children, path, query.Cursor);
        if (first < 0)
        {
            return ContextErrors.QueryInvalid.With($"the cursor '{(query.Cursor!.Length <= 80 ? query.Cursor : "(a long cursor)")}' is not a child of '{path}' (fix: use the nextCursor of the previous page, or none)");
        }

        // No cursor and no limit is the unpaged ask: every child, as before paging existed. Either one pages, 200 entries unless a limit says otherwise.
        var limit = query.Cursor is null && query.Limit is null ? children.Count : Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var page = children.Skip(first).Take(limit).ToList();
        var entries = page.Select(c => FileMapRollup.Entry(c, tree, links)).ToList();
        var next = first + page.Count < children.Count ? page[^1].Path : null;
        var suggested = page.SelectMany(c => Enumerable.Range(c.Start, c.End - c.Start)).SelectMany(i => links.Suggestions(tree.Files[i])).ToList();
        var totals = Totals(Enumerable.Range(range.Start, range.End - range.Start).Select(i => tree.Files[i]), links);
        return new FileMapView(1, query.Repo, basis.Sha, basis.MapVersion, query.Draft, path, entries,
            [.. entries.Where(e => e.Status == FileMapStatus.Unmapped).Select(e => new FileMapUnmapped(e.Path, e.Files))], suggested, totals, LensSymbols.Unavailable, next, children.Count);
    }

    // The index of the first child after the cursor (0 without one), or -1 when the cursor names no child of the path. A cursor is the path of the last entry of the previous page.
    private static int FirstAfter(List<TreeChild> children, string path, string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return 0;
        }

        var rest = cursor.StartsWith(path, StringComparison.Ordinal) ? cursor[path.Length..] : string.Empty;
        var directory = rest.EndsWith('/');
        var name = directory ? rest[..^1] : rest;
        if (name.Length == 0 || name.Contains('/', StringComparison.Ordinal))
        {
            return -1;
        }

        var index = children.FindIndex(c => c.IsDirectory == directory && string.Equals(c.Name, name, StringComparison.Ordinal));
        if (index >= 0)
        {
            return index + 1;
        }

        // The entry may have left the list (a filter, a newer commit): resume at the first child that sorts after it.
        var after = children.FindIndex(c => c.IsDirectory == directory ? string.CompareOrdinal(c.Name, name) > 0 : directory);
        return after < 0 ? children.Count : after;
    }

    /// <summary>Counts by status over <paramref name="under"/>: the one rollup 'context filemap' and a lens answer's map coverage share.</summary>
    internal static FileMapTotals Totals(IEnumerable<string> under, FileLinks tree)
    {
        int files = 0, mapped = 0, suggested = 0;
        foreach (var file in under)
        {
            files++;
            switch (tree.StatusOf(file))
            {
                case FileMapStatus.Mapped:
                    mapped++;
                    break;
                case FileMapStatus.Suggested:
                    suggested++;
                    break;
            }
        }

        return new FileMapTotals(files, mapped, suggested, files - mapped - suggested);
    }
}
