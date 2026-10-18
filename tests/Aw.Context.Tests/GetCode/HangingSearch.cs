using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A search that never answers until it is cancelled (the deadline).</summary>
internal sealed class HangingSearch : ICodeSearch
{
    public async ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return new CodeSearchHits([], Truncated: false);
    }

    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(commit);
}
