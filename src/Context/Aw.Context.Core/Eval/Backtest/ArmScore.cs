namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One arm's means over its samples (macro: every sample counts once).</summary>
/// <param name="Samples">The samples scored.</param>
/// <param name="Mapped">The samples that count in the node metrics.</param>
/// <param name="NodeRecall">Mean node recall; null when no sample is mapped (not measured, never 0).</param>
/// <param name="NodePrecision">Mean node precision; null as above.</param>
/// <param name="FileRecallAt20">Mean file recall@20; null when there are no samples.</param>
/// <param name="LeakyShare">The share of samples with any leaky input.</param>
/// <param name="Tokens">Model tokens spent on this arm (0 with the adjudicator off).</param>
public sealed record ArmScore(int Samples, int Mapped, double? NodeRecall, double? NodePrecision, double? FileRecallAt20, double LeakyShare, int Tokens)
{
    /// <summary>Precision with the adjudicator's labels counted as relevant; null when it did not run or stopped at its cap.</summary>
    public double? AdjudicatedPrecision { get; init; }
}
