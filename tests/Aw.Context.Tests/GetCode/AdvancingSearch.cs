using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A code search that answers like its inner one after <paramref name="advance"/> has moved the fake clock on, as a slow git grep would.</summary>
internal sealed class AdvancingSearch(ICodeSearch inner, Action advance) : ICodeSearch
{
    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        advance();
        return inner.FilesNaming(query, cancellationToken);
    }

    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken) => inner.Resolve(repo, commit, cancellationToken);
}
