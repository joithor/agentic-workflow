#pragma warning disable MA0182 // red stub: used by the implementation
namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads "git status --porcelain --untracked-files=all", leaving out untracked build output.</summary>
internal static class StatusPorcelain
{
    /// <summary>The entries that count as work: everything except an untracked file under a directory named like a build output.</summary>
    /// <param name="output">The porcelain output.</param>
    /// <param name="buildOutputs">Directory names (bin, obj, node_modules) whose untracked files are ignored.</param>
    /// <returns>The paths that keep a worktree.</returns>
    internal static IReadOnlyList<string> Significant(string output, IReadOnlyList<string> buildOutputs) => throw new NotSupportedException();
}
