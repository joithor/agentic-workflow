using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>One map held in memory: a backtest sample's as-of map. A draft read gets the same map; the backtest never asks for a draft.</summary>
internal sealed class FixedMap(VisibleMap map) : IMapProvider
{
    public ValueTask<Result<VisibleMap>> Read(ContextScope scope, MapRead read, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(map));
}
