namespace Aw.Context.Tests.QueryContext;

/// <summary>The backtest slot over nothing (the query tests never run a backtest).</summary>
internal sealed class NoBacktest : Aw.Kernel.Services.IService<Aw.Context.Core.Eval.Backtest.BacktestRequest, Aw.Context.Core.Eval.Backtest.BacktestRun>
{
    public ValueTask<Aw.Kernel.Result<Aw.Context.Core.Eval.Backtest.BacktestRun>> Handle(Aw.Context.Core.Eval.Backtest.BacktestRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Aw.Kernel.Result<Aw.Context.Core.Eval.Backtest.BacktestRun>>(Aw.Context.Core.ContextErrors.NoEligibleSample.With("this test world has no backtest"));
}
