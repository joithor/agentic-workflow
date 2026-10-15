#pragma warning disable MA0182 // red stub: used by the implementation
namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads the output of "git worktree list --porcelain".</summary>
internal static class PorcelainWorktrees
{
    /// <summary>Parses the blocks, one per worktree, the first being the main worktree.</summary>
    /// <param name="output">The porcelain output.</param>
    /// <returns>The worktrees in git's order.</returns>
    internal static IReadOnlyList<WorktreeEntry> Parse(string output) => throw new NotSupportedException();
}
