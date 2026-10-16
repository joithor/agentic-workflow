using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Reads a repo's first-parent history from git, never the working tree (S-7A). A repo git can't read is SND-CONTEXT-048.</summary>
public interface IMergeHistory
{
    /// <summary>The first-parent commits of the repo's current branch, oldest first, each merge with the files it changed against its first parent.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The history, or SND-CONTEXT-048.</returns>
    public ValueTask<Result<RepoHistory>> History(ContextRepo repo, CancellationToken cancellationToken);
}
