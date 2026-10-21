using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The engine's diff signal for an agent run (engine P8 D10): measured by the engine, never self-reported by the session.</summary>
public interface IRunDiff
{
    /// <summary>The commit the worktree's HEAD names; the launch records it as the run's start.</summary>
    /// <param name="worktree">The run's worktree.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The commit id; an error when the worktree has no HEAD.</returns>
    public ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken);

    /// <summary>Whether the run changed nothing: HEAD is still <paramref name="startHead"/> and the worktree has no change, staged, unstaged or untracked.</summary>
    /// <param name="worktree">The run's worktree.</param>
    /// <param name="startHead">What <see cref="Head"/> returned at the start.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when nothing changed; an error when the worktree can't be read.</returns>
    public ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken);

    /// <summary>The paths that differ from <paramref name="baseCommit"/>: committed, staged, unstaged and untracked, relative to the worktree.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <param name="baseCommit">The commit to compare with.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The paths in ordinal order, none twice; an error when the worktree or the commit can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ChangedPaths(string worktree, string baseCommit, CancellationToken cancellationToken);
}
