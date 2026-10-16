namespace Aw.Context.Core.Eval.Backtest;

/// <summary>A finished backtest: its report and where it was written.</summary>
/// <param name="Report">The report.</param>
/// <param name="Path">The report file under $AW_SINDRI_HOME/context/eval/backtest/.</param>
public sealed record BacktestRun(BacktestReport Report, string Path);
