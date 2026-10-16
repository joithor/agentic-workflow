using Aw.Context.Core.Docs;
using Aw.Context.Core.Eval.Backtest;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Stub.</summary>
internal sealed class OnyxBriefSource(ISourceCatalog catalog) : IBriefSource
{
    public ValueTask<Result<IReadOnlyList<BriefIssue>>> Issues(BriefScope scope, CancellationToken cancellationToken)
    {
        _ = catalog;
        throw new NotSupportedException();
    }

    public ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken) => throw new NotSupportedException();
}
