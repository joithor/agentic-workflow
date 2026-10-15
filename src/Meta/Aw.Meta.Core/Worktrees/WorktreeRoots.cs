#pragma warning disable MA0182 // red stub: used by the implementation
namespace Aw.Meta.Core.Worktrees;

/// <summary>The allowed roots: only a worktree strictly inside one of them may be removed.</summary>
internal static class WorktreeRoots
{
    /// <summary>Whether a path is inside a root (not the root itself, not a sibling that shares its prefix).</summary>
    /// <param name="roots">The allowed roots.</param>
    /// <param name="path">The worktree's directory.</param>
    /// <returns>True when the normalized path is under one of the roots.</returns>
    internal static bool Contains(IReadOnlyList<string> roots, string path) => throw new NotSupportedException();
}
