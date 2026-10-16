namespace Aw.Context.Tests.QueryContext;

/// <summary>The backtest slot over nothing (the query tests never run a backtest).</summary>
internal sealed class NoBacktest : Kernel.Services.IService<Context.Core.Eval.Backtest.BacktestRequest, Context.Core.Eval.Backtest.BacktestRun>
{
    public ValueTask<Kernel.Result<Context.Core.Eval.Backtest.BacktestRun>> Handle(Context.Core.Eval.Backtest.BacktestRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Kernel.Result<Context.Core.Eval.Backtest.BacktestRun>>(Context.Core.ContextErrors.NoEligibleSample.With("this test world has no backtest"));
}
