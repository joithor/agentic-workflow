using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>Whether a commit may be recorded as the next candidate of the current attempt (the parts that need no git).</summary>
public static class CandidateRule
{
    /// <summary>Checks room in the attempt, a new commit, and the hypothesis it pursues.</summary>
    /// <param name="state">The bug fix.</param>
    /// <param name="commit">The candidate commit.</param>
    /// <param name="hypothesis">The hypothesis number it pursues, if named.</param>
    /// <returns>Success, or AW-BUGFIX-011.</returns>
    public static Result<Unit> CanRecord(BugFixState state, string commit, int? hypothesis)
    {
        var cap = AttemptRule.Candidates(state.Mode);
        if (state.CurrentCandidates.Skip(cap - 1).Any())
        {
            return BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"mode {Names.Of(state.Mode ?? FixMode.A)} allows {cap} candidate(s) per attempt"));
        }

        if (state.Candidates.FirstOrDefault(c => c.Commit == commit) is { } duplicate)
        {
            return BugFixErrors.CandidateRefused.With($"commit {commit} is already candidate {duplicate.Id.Value}");
        }

        return Pursues(state, hypothesis);
    }

    private static Result<Unit> Pursues(BugFixState state, int? number)
    {
        if (number is not { } n)
        {
            return state.Mode == FixMode.B ? BugFixErrors.CandidateRefused.With("mode B candidates must name their hypothesis (--hypothesis <n>)") : Results.Ok();
        }

        if (state.Mode == FixMode.B && state.CurrentCandidates.FirstOrDefault(c => c.Hypothesis == n) is { } taken)
        {
            return BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"hypothesis {n} is already pursued by {taken.Id.Value} in this attempt"));
        }

        return state.Investigation?.Hypotheses.FirstOrDefault(h => h.Number == n) switch
        {
            null => BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"the handoff has no hypothesis {n}")),
            { Result: HypothesisResult.RuledOut } => BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"hypothesis {n} was ruled out")),
            _ => Results.Ok(),
        };
    }
}
