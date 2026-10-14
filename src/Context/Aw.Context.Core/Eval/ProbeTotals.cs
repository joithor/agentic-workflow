namespace Aw.Context.Core.Eval;

/// <summary>A run's totals: the means over its probes.</summary>
/// <param name="PrecisionAt5">The mean precision@5.</param>
/// <param name="CitationCorrectness">The mean citation correctness.</param>
/// <param name="Probes">How many probes ran.</param>
/// <param name="RunAt">When the run was.</param>
public sealed record ProbeTotals(double PrecisionAt5, double CitationCorrectness, int Probes, DateTimeOffset RunAt);
