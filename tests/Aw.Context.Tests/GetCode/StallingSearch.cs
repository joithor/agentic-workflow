using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A code search that answers like its inner one, except a query the predicate picks never answers: the profile's deadline decides it.</summary>
internal sealed class StallingSearch(ICodeSearch inner, Func<CodeSearchQuery, bool> stall) : ICodeSearch
{
    public async ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        if (stall(query))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        return await inner.FilesNaming(query, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken) => inner.Resolve(repo, commit, cancellationToken);
}
