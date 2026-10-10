using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The file-system adapter of <see cref="IRepoTree"/>. Read-only; unreadable files and directories read as
/// missing, so a permission problem shows as a failing criterion with evidence rather than a crash.</summary>
/// <param name="root">The absolute repo root.</param>
internal sealed class FileSystemRepoTree(string root) : IRepoTree
{
    private static readonly HashSet<string> _skipped = new(["bin", "obj", "node_modules"], StringComparer.Ordinal);

    public bool Exists(string path) => Unlinked(path) && (File.Exists(Full(path)) || Directory.Exists(Full(path)));

    /// <summary>The largest file a criterion reads; a bigger one reads as missing.</summary>
    internal const long MaxReadBytes = 1024 * 1024;

    public string? ReadText(string path)
    {
        try
        {
            var file = new FileInfo(Full(path));
            return Unlinked(path) && file.Exists && file.Length <= MaxReadBytes && Inside(file.FullName)
                ? File.ReadAllText(file.FullName)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Files(string directory, string suffix)
    {
        var start = Full(directory);
        var found = new List<string>();
        if (Unlinked(directory) && Directory.Exists(start))
        {
            Walk(start, suffix, found);
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private void Walk(string directory, string suffix, List<string> found)
    {
        try
        {
            found.AddRange(Directory.EnumerateFiles(directory)
                .Where(f => Path.GetFileName(f).EndsWith(suffix, StringComparison.Ordinal))
                .Select(Relative));
            foreach (var child in Directory.EnumerateDirectories(directory).Where(IsWalked))
            {
                Walk(child, suffix, found);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // why: an unreadable directory contributes nothing, like a missing one
        }
    }

    private static bool IsWalked(string directory)
    {
        var name = Path.GetFileName(directory);
        return !name.StartsWith('.') && !_skipped.Contains(name) && !new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private string Full(string path) => path.Length == 0 ? root : Path.Combine(root, path);

    // True when no segment of the path below the root is a symbolic link (or a '..'), so it can't lead outside.
    private bool Unlinked(string path)
    {
        var current = root;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }

            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null)
            {
                return false;
            }
        }

        return true;
    }

    private bool Inside(string full) =>
        full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private string Relative(string full) => Path.GetRelativePath(root, full).Replace('\\', '/');
}
