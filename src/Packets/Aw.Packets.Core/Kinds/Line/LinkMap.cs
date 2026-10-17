namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Builds the flattened link map the line manifests hand to the checks.</summary>
internal static class LinkMap
{
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Of(params (string Key, string Path)[] files) =>
        files.ToDictionary(static f => f.Key, static f => (IReadOnlyList<string>)[f.Path], StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> OfLists(
        (string Key, string Path)[] files, params (string Key, IReadOnlyList<string> Paths)[] lists)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, path) in files)
        {
            map[key] = [path];
        }

        foreach (var (key, paths) in lists)
        {
            map[key] = paths;
        }

        return map;
    }
}
