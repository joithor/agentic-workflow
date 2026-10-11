using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>What the /rootCause investigation handoff says.</summary>
/// <param name="Status">The handoff's status line (diagnosed, unfixed, …).</param>
/// <param name="Hypotheses">The hypotheses table.</param>
/// <param name="RootCause">The Root Cause section's text; empty when the section is missing.</param>
public sealed record Handoff(string Status, IReadOnlyList<Hypothesis> Hypotheses, string RootCause)
{
    /// <summary>Whether the handoff supports a fix: diagnosed, a confirmed hypothesis, a root cause. Never fix an unconfirmed cause.</summary>
    /// <returns>The handoff, or AW-BUGFIX-006.</returns>
    public Result<Handoff> SupportsFix()
    {
        if (Status != "diagnosed")
        {
            return BugFixErrors.HandoffRefused.With($"handoff status is \"{Status}\", expected \"diagnosed\" (run /rootCause --investigate-only)");
        }

        if (!Hypotheses.Any(static h => h.Result == HypothesisResult.Confirmed))
        {
            return BugFixErrors.HandoffRefused.With("no hypothesis is confirmed; ask the user before fixing an unconfirmed cause");
        }

        return RootCause.Length == 0 ? BugFixErrors.HandoffRefused.With("the handoff has no ## Root Cause text") : this;
    }
}
