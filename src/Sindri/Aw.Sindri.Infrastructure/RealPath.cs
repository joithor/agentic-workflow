namespace Aw.Sindri.Infrastructure;

/// <summary>
/// Resolves every symbolic link along a path, like realpath(3), so a home reached through a link (macOS's /var is
/// /private/var) is judged where it really is. Parts that don't exist yet are kept as written. '~' and '~/…' expand to
/// the user's home first (profile repo entries are written that way). Used by the prover-home rule and the repo locator.
/// </summary>
internal static class RealPath
{
    private const int MaxLinks = 40;

    /// <summary>The path with every link resolved, or null when the links loop or can't be read.</summary>
    internal static string? Of(string path)
    {
        try
        {
            return Resolve(Path.GetFullPath(Expanded(path)), MaxLinks);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Expanded(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;

    private static string? Resolve(string full, int budget)
    {
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, part);
            if (new FileInfo(next).LinkTarget is not null)
            {
                if (budget == 0)
                {
                    return null;
                }

                var target = new FileInfo(next).ResolveLinkTarget(returnFinalTarget: false);
                next = target is null ? next : Resolve(Path.GetFullPath(target.FullName), budget - 1);
                if (next is null)
                {
                    return null;
                }
            }

            current = next;
        }

        return current;
    }
}
