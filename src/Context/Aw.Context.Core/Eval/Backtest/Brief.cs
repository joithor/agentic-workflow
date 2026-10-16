namespace Aw.Context.Core.Eval.Backtest;

/// <summary>An issue's brief text, cleaned at the ingest boundary. Held in memory for the run, never written to a report.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="Text">The title and description.</param>
public sealed record Brief(string Key, string Text);
