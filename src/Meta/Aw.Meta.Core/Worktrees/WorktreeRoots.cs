using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Worktrees;

/// <summary>The allowed roots: only a worktree strictly inside one of them may be removed.</summary>
internal static class WorktreeRoots
{
    /// <summary>Whether a path is inside a root (not the root itself, not a sibling that shares its prefix).</summary>
    /// <param name="roots">The allowed roots.</param>
    /// <param name="path">The worktree's directory.</param>
    /// <returns>True when the normalized path is under one of the roots.</returns>
    internal static bool Contains(IReadOnlyList<string> roots, string path)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path);
        return roots.Any(root => Normalize(root) is { Length: > 0 } prefix && normalized.StartsWith(prefix + "/", StringComparison.Ordinal) && normalized.Length > prefix.Length + 1);
    }

    /// <summary>Resolves "." and ".." segments and drops trailing slashes of an absolute path; any other text comes back trimmed.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path.</returns>
    internal static string Normalize(string path) => WorktreeConfig.NormalizePath(path);
}
