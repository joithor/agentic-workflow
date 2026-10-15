namespace Aw.Context.Core.Lenses;

/// <summary>Repo paths as people write them: './src', '/src/', 'src//tabs' and '.' all name what git lists as 'src', 'src', 'src/tabs' and the root.</summary>
public static class RepoPaths
{
    /// <summary>Drops empty and '.' segments, reads '\' as '/', and resolves '..' inside the path; a '..' that climbs above the root stays, so the path names nothing.</summary>
    /// <param name="path">The path as written (a trailing '/' is dropped too).</param>
    /// <returns>The path from the repo root, with '/' between segments; empty for the root.</returns>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var kept = new List<string>();
        foreach (var segment in path.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal) && kept.Count > 0 && !string.Equals(kept[^1], "..", StringComparison.Ordinal))
            {
                kept.RemoveAt(kept.Count - 1);
            }
            else
            {
                kept.Add(segment);
            }
        }

        return string.Join('/', kept);
    }
}
