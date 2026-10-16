using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Stub.</summary>
internal sealed class GitMergeHistory(GitCli git) : IMergeHistory
{
    public ValueTask<Result<RepoHistory>> History(ContextRepo repo, CancellationToken cancellationToken)
    {
        _ = git;
        throw new NotSupportedException();
    }
}
