using Aw.Context.Core.Results;

namespace Aw.Context.Core.Eval;

/// <summary>Scores one answer against a probe (spec §9.2 item 3): precision@5 and citation correctness. Pure.</summary>
public static class ProbeScoring
{
    /// <summary>How many of the answer's keys are scored.</summary>
    public const int K = 5;

    /// <summary>The keys an answer names, in order, as scoring reads them: each item's node ('code:P' as 'file:P'), the nodes on an affects item's path, then its citations' keys ('repo:&lt;repo&gt;/P' as 'file:P'). Duplicates are dropped.</summary>
    /// <param name="result">The answer.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlyList<string> Keys(ContextResult result) => throw new NotSupportedException();

    /// <summary>Scores <paramref name="result"/> against <paramref name="probe"/>.</summary>
    /// <param name="probe">The probe.</param>
    /// <param name="result">Its answer.</param>
    /// <returns>The score.</returns>
    public static ProbeScore Score(ProbeCase probe, ContextResult result) => throw new NotSupportedException();
}
