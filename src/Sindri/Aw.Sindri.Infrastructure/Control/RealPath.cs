namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// A path with every symbolic link resolved, component by component, like realpath(3): on macOS '/var/folders/…' is
/// '/private/var/folders/…'. A component that doesn't exist or can't be read is kept as written.
/// </summary>
internal static class RealPath
{
    private const int MaxLinks = 40;

    /// <summary>The real path of <paramref name="path"/> ('~' and '~/…' expanded to the home directory).</summary>
    /// <param name="path">An absolute path, or one relative to the working directory.</param>
    /// <returns>The resolved absolute path.</returns>
    internal static string Of(string path) => Resolve(Path.GetFullPath(Expanded(path)), MaxLinks);

    private static string Expanded(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;

    private static string Resolve(string full, int links)
    {
        var current = "/";
        foreach (var part in full.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Join(current, part);
            var target = links > 0 ? LinkTarget(next) : null;
            current = target is null ? next : Resolve(Path.GetFullPath(target, current), links - 1);
        }

        return current;
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
