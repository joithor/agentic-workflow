namespace Aw.Context.Core.Eval.Backtest;

/// <summary>A predicted node no PR touched, as the adjudicator sees it: its key (kept here) and its label (sent).</summary>
/// <param name="Key">The node key.</param>
/// <param name="Label">The node label.</param>
public sealed record Surface(string Key, string Label);
