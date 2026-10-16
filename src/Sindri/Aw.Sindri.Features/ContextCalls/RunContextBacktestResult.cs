namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What a backtest job did, small by design (the job's result is capped at 16 KiB): counts, the pass line and where the full report is. Never a brief's
/// text, a sample or a score table: the report file holds those.
/// </summary>
/// <param name="Samples">The samples scored.</param>
/// <param name="SmallSample">True below 30 samples: the pass line decides nothing.</param>
/// <param name="LeftOut">The issues that were not samples.</param>
/// <param name="Arms">One line per arm, in report order.</param>
/// <param name="PassLine">The overall §9.4 line, or null when no arm ran.</param>
/// <param name="Report">The full report's path under $AW_SINDRI_HOME/context/eval/backtest/.</param>
/// <param name="Degraded">Notes on what limited the run, at most 20.</param>
public sealed record RunContextBacktestResult(int Samples, bool SmallSample, int LeftOut, IReadOnlyList<BacktestArmLine> Arms, string? PassLine, string Report, IReadOnlyList<string> Degraded);
