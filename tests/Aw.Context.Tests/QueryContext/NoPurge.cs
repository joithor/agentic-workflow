using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The eval purge slot over nothing.</summary>
internal sealed class NoPurge : IService<EvalPurgeRequest, EvalPurgeReport>
{
    public ValueTask<Result<EvalPurgeReport>> Handle(EvalPurgeRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<EvalPurgeReport>>(new EvalPurgeReport(0, 0, 0));
}
