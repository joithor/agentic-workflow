namespace Aw.Context.Core.Lenses;

/// <summary>The direct children of a path with the worst status under each and the features and intent on them (spec §8.9).</summary>
internal static class FileMapRollup
{
    private const int MaxFeatures = 10;

    /// <summary>The children, directories first, then by name.</summary>
    /// <param name="path">The directory listed ('' or ending in '/').</param>
    /// <param name="under">Every file under it, ordered.</param>
    /// <param name="links">The graph's file links.</param>
    /// <returns>The entries.</returns>
    internal static List<FileMapEntry> Entries(string path, List<string> under, FileLinks links) =>
        [.. under.GroupBy(f => Child(path, f), StringComparer.Ordinal).Select(g => Entry(g.Key, [.. g], links))
            .OrderByDescending(e => e.Type == FileEntryType.Dir).ThenBy(e => e.Name, StringComparer.Ordinal)];

    private static FileMapEntry Entry(string child, List<string> files, FileLinks links)
    {
        var directory = child.EndsWith('/');
        var name = child.TrimEnd('/')[(child.TrimEnd('/').LastIndexOf('/') + 1)..];
        var features = (directory ? files.SelectMany(f => links.FeaturesOf(f, confirmedOnly: true)) : links.FeaturesOf(files[0], confirmedOnly: false))
            .GroupBy(f => f.Key.Value, StringComparer.Ordinal).Select(g => g.OrderBy(f => f.Edge).First()).OrderBy(f => f.Key.Value, StringComparer.Ordinal).ToList();
        return new FileMapEntry(name, child, directory ? FileEntryType.Dir : FileEntryType.File, files.Max(links.StatusOf), files.Count, [.. features.Take(MaxFeatures)],
            features.Count == 0 ? null : links.IntentOn(features.Select(f => f.Key)));
    }

    // The direct child of a directory that holds a file: 'src/tabs/' for 'src/tabs/a/b.ts' under 'src/', the file itself when it sits right there.
    private static string Child(string path, string file)
    {
        var rest = file[path.Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? file : path + rest[..(slash + 1)];
    }
}
