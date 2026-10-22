namespace Aw.Context.Core.Lenses;

/// <summary>Scope nodes with code behind them. Only a confirmed 'implements' edge counts as implemented (not 'tests', not a model suggestion).</summary>
/// <param name="Implemented">Nodes with a confirmed 'implements' edge.</param>
/// <param name="Of">Scope size.</param>
/// <param name="Level">'file' until the code index answers for the lenses' files, then 'symbol' (Phase 9).</param>
/// <param name="Suggested">Nodes whose only 'implements' edges are unconfirmed.</param>
public sealed record CoverageCode(int Implemented, int Of, string Level, int Suggested);
