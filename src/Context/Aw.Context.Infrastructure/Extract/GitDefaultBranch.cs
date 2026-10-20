using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Stub (red).</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitDefaultBranch(GitCli git) : IDefaultBranch
{
    public ValueTask<Result<bool>> Reaches(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"F1: not built yet {git}");
}
