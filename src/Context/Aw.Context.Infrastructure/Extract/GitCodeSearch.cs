using Aw.Context.Core.Code;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Searches a repo's files at a commit through 'git grep' (never the working tree), leaving out the paths the repo denies.</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitCodeSearch(GitCli git) : ICodeSearch
{
    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        _ = git;
        throw new NotSupportedException();
    }
}
