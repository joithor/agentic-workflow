namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// A tiny decision stub standing in for P3's TransitionDecision (PR #180, not on main): the first transition from the
/// state on 'step.completed' whose outcome matches; identical findings to the previous round park 'no-progress' (rev 7.2
/// §3.7: the digest of the whole findings value); re-entering a loop's state past its cap parks 'rounds'; no match parks
/// 'needs-answer'. Pure, so it is safe in workflow code.
/// </summary>
internal static class SpikeDecision
{
    private const string Findings = "fact:findings";

    /// <summary>Decides <paramref name="report"/> from <paramref name="from"/>.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="from">The current state.</param>
    /// <param name="report">The Step's report.</param>
    /// <param name="rounds">The loops' current rounds.</param>
    /// <param name="lastFindings">The previous round's findings.</param>
    /// <returns>The verdict.</returns>
    public static SpikeVerdict Decide(SpikeRecipe recipe, string from, SpikeReport report, IReadOnlyDictionary<string, int> rounds, string? lastFindings)
    {
        var transition = recipe.Transitions.FirstOrDefault(t => Matches(t, from, report));
        if (transition is null)
        {
            return new SpikeVerdict(from, "needs-answer", Loop: null, Round: 0, lastFindings);
        }

        var findings = report.Outputs.GetValueOrDefault(Findings);
        if (findings is not null && string.Equals(findings, lastFindings, StringComparison.Ordinal))
        {
            return new SpikeVerdict(transition.To, "no-progress", Loop: null, Round: 0, findings);
        }

        return Enter(recipe, transition.To, rounds, findings ?? lastFindings);
    }

    private static bool Matches(SpikeTransition t, string from, SpikeReport report) =>
        t.From.Contains(from, StringComparer.Ordinal) && t.On == "step.completed" && (t.Outcome is null || t.Outcome == report.Outcome);

    private static SpikeVerdict Enter(SpikeRecipe recipe, string to, IReadOnlyDictionary<string, int> rounds, string? findings)
    {
        if (recipe.State(to).Loop is not { } loop)
        {
            return new SpikeVerdict(to, Park: null, Loop: null, Round: 0, findings);
        }

        var round = rounds.GetValueOrDefault(loop) + 1;
        return new SpikeVerdict(to, round > recipe.LoopCaps[loop] ? "rounds" : null, loop, round, findings);
    }
}
