namespace Aw.Context.Core.Lenses;

/// <summary>How well the anchor's scope is backed; the scope count agrees with 'context map status' for the same subtree.</summary>
/// <param name="Scope">Nodes in the anchor's own scope.</param>
/// <param name="Covered">Of them, those with intent behind them ('mentions' or 'decides' edges).</param>
/// <param name="Confirmed">Of them, those a human or the extractor stands behind.</param>
/// <param name="Implemented">Of them, those with an 'implements' or 'tests' edge from a file.</param>
public sealed record LensCoverage(int Scope, int Covered, int Confirmed, int Implemented);
