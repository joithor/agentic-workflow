namespace Aw.Context.Core.Eval;

/// <summary>A run's totals: the means over its probes.</summary>
/// <param name="PrecisionAt5">The mean precision@5.</param>
/// <param name="CitationCorrectness">The mean citation correctness.</param>
/// <param name="Probes">How many probes ran.</param>
/// <param name="RunAt">When the run was.</param>
/// <param name="AbsentReturned">How many absent nodes (a probe's 'absentNodes') the answers named anyway, over all probes; a probe naming more than the same probe did in the baseline is a regression. 0 in a report written before this count existed.</param>
public sealed record ProbeTotals(double PrecisionAt5, double CitationCorrectness, int Probes, DateTimeOffset RunAt, int AbsentReturned = 0);
