using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>The landed changes of a set of paths over <see cref="GitCli"/> (read-only git, the caller's GIT_* variables removed, never the working tree).</summary>
internal sealed class GitFileHistory(GitCli git) : IFileHistory
{
    public async ValueTask<Result<FileHistory>> Changes(ContextRepo repo, FileHistoryQuery query, CancellationToken cancellationToken)
    {
        _ = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        throw new NotSupportedException();
    }
}
