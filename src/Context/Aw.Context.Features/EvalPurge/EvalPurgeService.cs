using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalPurge;

/// <summary>Deletes eval reports and the as-of map cache past 90 days (or everything, probe baselines included, with All). A human or service call.</summary>
internal sealed class EvalPurgeService(IEvalStore store, TimeProvider clock) : IService<EvalPurgeRequest, EvalPurgeReport>
{
    public ValueTask<Result<EvalPurgeReport>> Handle(EvalPurgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Scope.Caller.Kind == ContextCallerKind.Session
            ? ValueTask.FromResult<Result<EvalPurgeReport>>(ContextErrors.SessionsDisabled.With("purging eval files is a human or service call: a session can't"))
            : store.Purge(clock.GetUtcNow(), request.All, cancellationToken);
    }
}
