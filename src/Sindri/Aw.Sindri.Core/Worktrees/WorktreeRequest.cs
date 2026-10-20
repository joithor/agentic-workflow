using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>What <see cref="IWorktrees.Create"/> is asked for.</summary>
/// <param name="RepoRoot">The repo's main checkout, an absolute path.</param>
/// <param name="BaseCommit">The full 40-hex commit sha the branch starts at; never a ref.</param>
/// <param name="Branch">The branch the worktree checks out (created at the base when it does not exist).</param>
/// <param name="Path">Where the worktree goes, an absolute path.</param>
public sealed record WorktreeRequest(string RepoRoot, string BaseCommit, string Branch, string Path)
{
    /// <summary>Checks the request without touching the disk.</summary>
    /// <returns>SND-WT-001 naming the first problem, or null when the request is well formed.</returns>
    public Error? Validate() => throw new NotSupportedException();
}
