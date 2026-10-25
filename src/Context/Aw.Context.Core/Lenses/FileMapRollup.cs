namespace Aw.Context.Core.Lenses;

/// <summary>The direct children of a path with the worst status under each and the features and intent on them (spec §8.9).</summary>
internal static class FileMapRollup
{
    private const int MaxFeatures = 10;

    /// <summary>The worst status under a child: its own for a file, the worst of its files' for a directory.</summary>
    internal static FileMapStatus StatusOf(TreeChild child, RepoTree tree, FileLinks links)
    {
        var status = FileMapStatus.Mapped;
        for (var i = child.Start; i < child.End && status != FileMapStatus.Unmapped; i++)
        {
            var own = links.StatusOf(tree.Files[i]);
            status = own > status ? own : status;
        }

        return status;
    }

    /// <summary>One child as an entry: the status, the features and the intent, read from the files under it only.</summary>
    internal static FileMapEntry Entry(TreeChild child, RepoTree tree, FileLinks links)
    {
        var files = Enumerable.Range(child.Start, child.End - child.Start).Select(i => tree.Files[i]).ToList();
        var features = (child.IsDirectory ? files.SelectMany(f => links.FeaturesOf(f, confirmedOnly: true)) : links.FeaturesOf(files[0], confirmedOnly: false))
            .GroupBy(f => f.Key.Value, StringComparer.Ordinal).Select(g => g.OrderBy(f => f.Edge).First()).OrderBy(f => f.Key.Value, StringComparer.Ordinal).ToList();
        return new FileMapEntry(child.Name, child.Path, child.IsDirectory ? FileEntryType.Dir : FileEntryType.File, StatusOf(child, tree, links), files.Count, [.. features.Take(MaxFeatures)],
            features.Count == 0 ? null : links.IntentOn(features.Select(f => f.Key)));
    }
}
