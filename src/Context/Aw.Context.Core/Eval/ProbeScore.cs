namespace Aw.Context.Core.Eval;

/// <summary>One probe's score.</summary>
/// <param name="Id">The probe's id.</param>
/// <param name="PrecisionAt5">Expected keys among the answer's first five, over min(5, keys returned); 0 when nothing came back.</param>
/// <param name="CitationCorrectness">The share of the answer's items whose every citation marker resolves to a citation with a version (1 when there are no items).</param>
/// <param name="Top">The answer's first five keys, as scored ('file:' for repo paths).</param>
/// <param name="Hits">Which of <paramref name="Top"/> were expected.</param>
/// <param name="AbsentReturned">Absent nodes the answer named anyway.</param>
/// <param name="Error">The query's error code when it failed (then both numbers are 0), else null.</param>
public sealed record ProbeScore(ProbeId Id, double PrecisionAt5, double CitationCorrectness, IReadOnlyList<string> Top, IReadOnlyList<string> Hits,
    IReadOnlyList<string> AbsentReturned, string? Error = null);
