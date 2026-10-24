using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>
/// Git worktrees for the engine (port; engine spec §6.5 per-run worktrees, 'step.phase.seed' phase worktrees). It never
/// fetches, never forces and never resets: a checkout that is not exactly what was asked for is an error, and a dirty
/// tree is never discarded.
/// </summary>
public interface IWorktrees
{
    /// <summary>
    /// Creates the worktree on <see cref="WorktreeRequest.Branch"/> at <see cref="WorktreeRequest.BaseCommit"/>. Idempotent: the
    /// same path and branch with the checkout still at the same base returns the existing checkout. Commits made in it, a detached HEAD or another branch since are SND-WT-002.
    /// </summary>
    /// <param name="request">The repo, base, branch and path.</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    /// <returns>
    /// The checkout; SND-WT-001 for a request that is not valid; SND-WT-002 when the path or the branch is already in use at a
    /// different base or branch (nothing is reset); SND-WT-003 when git fails (the repo or the commit is missing).
    /// </returns>
    public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a branch of the repo to the commit it points at, so a stacked phase can start at its parent phase's head (line spec §8.3). It never fetches:
    /// the local branch is looked up first, then the same name on 'origin'.
    /// </summary>
    /// <param name="repoRoot">The repo's main checkout, an absolute path.</param>
    /// <param name="branch">The branch name, without 'refs/heads/'.</param>
    /// <param name="cancellationToken">Cancels the git call.</param>
    /// <returns>The full 40-character commit sha; SND-WT-001 for a repo root that is not absolute or a branch name that is empty or starts with '-'; SND-WT-003 when git fails or the branch is not known here.</returns>
    public ValueTask<Result<string>> Resolve(string repoRoot, string branch, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the worktree at <paramref name="path"/> ('git worktree remove', never with force) and prunes. Removing a path that
    /// is already gone succeeds.
    /// </summary>
    /// <param name="path">The worktree's absolute path.</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    /// <returns>Success; SND-WT-001 for a relative path; SND-WT-004 when the tree has uncommitted or untracked files (left as it is); SND-WT-003 when git fails.</returns>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken);
}
