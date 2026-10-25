namespace Aw.CodeIndex.Core.Planning;

/// <summary>Finds the directories that hold a project manifest, the 'same project' ranking the call resolver uses (spec §5.4).</summary>
public static class ProjectManifests
{
    private static readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "go.mod", "Cargo.toml", "pyproject.toml", "setup.py", "pom.xml", "build.gradle", "build.gradle.kts", "Gemfile", "composer.json", "Package.swift",
    };

    /// <summary>Lists the manifest directories of a file list.</summary>
    /// <param name="paths">Every repo-relative path of the snapshot, with '/' separators.</param>
    /// <returns>Each directory that holds a manifest (a '.csproj' or a known manifest name), repo-relative with no trailing slash; the root is the empty string.</returns>
    public static IReadOnlySet<string> Directories(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var slash = path.LastIndexOf('/');
            var name = path[(slash + 1)..];
            if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || _names.Contains(name))
            {
                _ = directories.Add(slash < 0 ? string.Empty : path[..slash]);
            }
        }

        return directories;
    }
}
