using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Removes the worktrees the rules allow, one at a time with "git worktree remove" (never forced), and records what it did.</summary>
public sealed class WorktreeSweeper
{
    /// <summary>Sweeps a scope.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="scope">Which worktrees to consider.</param>
    /// <param name="mode">Remove, or only report.</param>
    /// <param name="trigger">Names who asked (sweep, merge-run, watch); goes into the log line.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed and kept, or an error when nothing could be judged.</returns>
    public ValueTask<Result<SweepOutcome>> Sweep(TeamConfig team, WorktreeScope scope, SweepMode mode, string trigger, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <summary>The sweep a trigger runs for pull requests that ended: removes (or, with the config's Enabled false, only reports), logs a line and keeps it as the last sweep.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="prs">The pull requests that merged or closed.</param>
    /// <param name="trigger">merge-run or watch.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The one summary line (empty when no worktree belongs to those pull requests), or an error.</returns>
    public ValueTask<Result<string>> Triggered(TeamConfig team, IReadOnlyCollection<int> prs, string trigger, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
