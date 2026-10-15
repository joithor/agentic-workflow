namespace Aw.Context.Core.Lenses;

/// <summary>Repo paths as people write them: './src', '/src/', 'src//tabs' and '.' all name what git lists as 'src', 'src', 'src/tabs' and the root.</summary>
public static class RepoPaths
{
    /// <summary>Drops empty and '.' segments, so a leading './' or '/' and a doubled '/' mean nothing.</summary>
    /// <param name="path">The path as written (a trailing '/' is dropped too).</param>
    /// <returns>The path from the repo root, with '/' between segments; empty for the root.</returns>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Join('/', path.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => !string.Equals(segment, ".", StringComparison.Ordinal)));
    }
}
