using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalPurge;

/// <summary>Deletes eval reports and the as-of map cache past 90 days (or everything, probe baselines included, with All). Any caller, a session included.</summary>
internal sealed class EvalPurgeService(IEvalStore store, TimeProvider clock) : IService<EvalPurgeRequest, EvalPurgeReport>
{
    public ValueTask<Result<EvalPurgeReport>> Handle(EvalPurgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.Purge(clock.GetUtcNow(), request.All, cancellationToken);
    }
}
