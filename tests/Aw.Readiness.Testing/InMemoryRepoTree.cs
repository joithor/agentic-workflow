using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The in-memory fake of <see cref="IRepoTree"/>: files are a path → text map; directories exist when a file
/// is under them. Same skip rules as the file-system adapter (RepoTreeContract keeps them equal).</summary>
public sealed class InMemoryRepoTree : IRepoTree
{
    private static readonly string[] _skippedSegments = ["bin", "obj", "node_modules"];
    private readonly SortedDictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a file.</summary>
    /// <param name="path">A repo-relative path with '/'.</param>
    /// <param name="text">Its text.</param>
    /// <returns>This tree, for chaining.</returns>
    public InMemoryRepoTree With(string path, string text = "")
    {
        _files[path] = text;
        return this;
    }

    /// <summary>Removes a file.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>This tree, for chaining.</returns>
    public InMemoryRepoTree Without(string path)
    {
        _ = _files.Remove(path);
        return this;
    }

    /// <inheritdoc/>
    public bool Exists(string path) =>
        _files.ContainsKey(path) || _files.Keys.Any(f => f.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal));

    /// <inheritdoc/>
    public string? ReadText(string path) => _files.GetValueOrDefault(path);

    /// <inheritdoc/>
    public IReadOnlyList<string> Files(string directory, string suffix)
    {
        var prefix = directory.Length == 0 ? "" : directory.TrimEnd('/') + "/";
        return [.. _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)
            && f.EndsWith(suffix, StringComparison.Ordinal)
            && !IsSkipped(f[prefix.Length..]))];
    }

    private static bool IsSkipped(string relative)
    {
        var segments = relative.Split('/');
        return segments[..^1].Any(s => s.StartsWith('.') || _skippedSegments.Contains(s, StringComparer.Ordinal));
    }
}
