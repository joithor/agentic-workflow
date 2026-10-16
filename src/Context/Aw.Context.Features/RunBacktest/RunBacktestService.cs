using Aw.Context.Core.Eval.Backtest;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Stub.</summary>
internal sealed class RunBacktestService : IService<BacktestRequest, BacktestRun>
{
    public ValueTask<Result<BacktestRun>> Handle(BacktestRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
