using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalPurge;

/// <summary>Stub.</summary>
internal sealed class EvalPurgeService : IService<EvalPurgeRequest, EvalPurgeReport>
{
    public ValueTask<Result<EvalPurgeReport>> Handle(EvalPurgeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
