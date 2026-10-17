namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One arm against the §9.4 bars. Each bar is a <see cref="Verdicts"/> word.</summary>
/// <param name="NodeRecall">Recall at least 0.60 and above the brief-only arm.</param>
/// <param name="NodePrecision">Precision at least 0.60.</param>
/// <param name="FileRecallAt20">File recall@20 at least 0.50.</param>
/// <param name="Tokens">At most 1.5M tokens.</param>
/// <param name="Overall">'PASS', 'NOT PASSED', 'not measured' or the owner-decision line below 30 samples.</param>
public sealed record ArmPassLine(string NodeRecall, string NodePrecision, string FileRecallAt20, string Tokens, string Overall);
