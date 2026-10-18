using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A search that fails with 048 for the queries <paramref name="fails"/> picks and passes the rest on.</summary>
internal sealed class FailingSearch(ICodeSearch inner, Func<CodeSearchQuery, bool> fails) : ICodeSearch
{
    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken) =>
        fails(query) ? ValueTask.FromResult<Result<CodeSearchHits>>(ContextErrors.ExtractionUnreadable.With("the test search failed")) : inner.FilesNaming(query, cancellationToken);
}
