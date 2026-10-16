namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The eligible samples and what was left out.</summary>
/// <param name="Samples">The samples, by issue key.</param>
/// <param name="LeftOut">The issues that were not samples.</param>
public sealed record Corpus(IReadOnlyList<CorpusSample> Samples, LeftOut LeftOut);
