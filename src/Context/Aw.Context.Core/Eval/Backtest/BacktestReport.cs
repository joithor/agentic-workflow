namespace Aw.Context.Core.Eval.Backtest;

/// <summary>
/// A backtest run (spec §9.1), kept under $AW_SINDRI_HOME/context/eval/backtest/. It holds refs, versions, shas, key lists and numbers: never a brief's text.
/// </summary>
/// <param name="SchemaVersion">The report format (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="CreatedAt">When the run started.</param>
/// <param name="Window">The window the samples were cut to ('2026-07-01..2026-07-12', 'all' or 'first 10'), as asked.</param>
/// <param name="SampleCount">The samples scored.</param>
/// <param name="SmallSample">True below 30 samples: the pass line decides nothing (spec §9.1).</param>
/// <param name="LeakyShare">The share of samples whose brief was edited after the cut.</param>
/// <param name="LeftOut">The issues that were not samples, by reason.</param>
/// <param name="Samples">The samples: refs, versions, cut and merge shas, truth files.</param>
/// <param name="Arms">The arm rows, in report order, the not-yet-available ones included.</param>
/// <param name="Overall">The pass line for the arm 'all', or for the best-run arm when it didn't run: the §9.4 verdict for the record.</param>
/// <param name="Degraded">Notes on what limited the run ('adjudication stopped at the token cap', 'N of M sample maps built without clusters').</param>
public sealed record BacktestReport(int SchemaVersion, DateTimeOffset CreatedAt, string Window, int SampleCount, bool SmallSample, double LeakyShare, LeftOut LeftOut,
    IReadOnlyList<CorpusSample> Samples, IReadOnlyList<ArmReport> Arms, string? Overall, IReadOnlyList<string> Degraded)
{
    /// <summary>The report format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}
