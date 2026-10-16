namespace Aw.Context.Core.Lenses;

/// <summary>Scope nodes with intent behind them ('mentions' or 'decides' edges).</summary>
/// <param name="Covered">Covered nodes.</param>
/// <param name="Of">Scope size.</param>
public sealed record CoverageIntent(int Covered, int Of);
