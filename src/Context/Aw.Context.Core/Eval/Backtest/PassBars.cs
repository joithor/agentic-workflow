namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The §9.4 bars. S-7B re-sets them through the ledger.</summary>
public static class PassBars
{
    /// <summary>Node recall at least this, and above the brief-only arm.</summary>
    public const double NodeRecall = 0.60;

    /// <summary>Node precision at least this.</summary>
    public const double NodePrecision = 0.60;

    /// <summary>File recall@20 at least this.</summary>
    public const double FileRecallAt20 = 0.50;

    /// <summary>Model tokens at most this per run (also the adjudicator's cap).</summary>
    public const int MaxTokens = 1_500_000;

    /// <summary>Below this many samples the pass line decides nothing.</summary>
    public const int MinSamples = 30;

    /// <summary>The ranked files scored for file recall.</summary>
    public const int FileCutoff = 20;
}
