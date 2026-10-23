using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The local git work of a restack (PB spec §4.6): fetch the new base, rebase the workspace's branch onto it without the commits the old base brought, and put the workspace back when a check
/// refuses the result. Nothing here reaches the forge except the fetch; the push is the forge port's.
/// </summary>
public interface IWorkspaceRebase
{
    /// <summary>Fetches a branch from the workspace's 'origin' and answers the commit it points at.</summary>
    /// <param name="worktree">The workspace.</param>
    /// <param name="branch">The branch name, such as 'main'.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The branch's commit; an error when the branch can't be fetched.</returns>
    public ValueTask<Result<string>> Fetch(string worktree, string branch, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="ancestor"/> is in the history of <paramref name="descendant"/>.</summary>
    /// <param name="worktree">The workspace.</param>
    /// <param name="ancestor">The older commit.</param>
    /// <param name="descendant">The newer commit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when it is; an error when a commit is unknown.</returns>
    public ValueTask<Result<bool>> Contains(string worktree, string ancestor, string descendant, CancellationToken cancellationToken);

    /// <summary>
    /// Rebases the workspace's branch onto <paramref name="onto"/>. With <paramref name="from"/> only the commits after it are replayed, so the commits the old base brought are dropped even when the base was
    /// squash-merged (the patches match but the shas don't); with none, git drops the commits whose patch <paramref name="onto"/> already has. A conflict is abandoned and leaves the workspace as it was.
    /// </summary>
    /// <param name="worktree">The workspace, clean.</param>
    /// <param name="onto">The commit to rebase onto.</param>
    /// <param name="from">The old base's tip: the commits after it are the branch's own; or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new head, or the conflicted paths; an error when git itself fails.</returns>
    public ValueTask<Result<WorkspaceRebased>> Rebase(string worktree, string onto, string? from, CancellationToken cancellationToken);

    /// <summary>Puts the workspace's branch and files back on <paramref name="sha"/>.</summary>
    /// <param name="worktree">The workspace.</param>
    /// <param name="sha">The commit to go back to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Reset(string worktree, string sha, CancellationToken cancellationToken);
}
