using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Stub.</summary>
internal sealed class AsOfMapBuilder(IRepoReader repos, IClusterSource clusters, IGraphStore store, IEvalStore cache)
{
    public ValueTask<Result<AsOfGraph>> Build(AsOfRequest request, CancellationToken cancellationToken)
    {
        _ = (request, cancellationToken, repos, clusters, store, cache);
        throw new NotSupportedException();
    }
}
