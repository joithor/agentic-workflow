using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>When a new attempt may start, and how many candidates an attempt runs: one decision for start-attempt and resume.</summary>
public static class AttemptRule
{
    /// <summary>How many candidates an attempt in <paramref name="mode"/> runs: two competing ones in mode B, otherwise one.</summary>
    /// <param name="mode">The attempt's mode.</param>
    /// <returns>1 or 2.</returns>
    public static int Candidates(FixMode? mode) => mode == FixMode.B ? 2 : 1;

    /// <summary>Whether the next attempt may start in <paramref name="mode"/>.</summary>
    /// <param name="state">The bug fix.</param>
    /// <param name="mode">The requested mode.</param>
    /// <returns>Success, or AW-BUGFIX-010 saying why not.</returns>
    public static Result<Unit> CanStart(BugFixState state, FixMode mode)
    {
        var current = state.CurrentCandidates.ToList();
        if (current.Find(static c => c.Resolves) is { } resolving)
        {
            return BugFixErrors.AttemptRefused.With($"candidate {resolving.Id.Value} already resolves the ticket; run advance report --candidate {resolving.Id.Value}");
        }

        if (current.Find(static c => !c.Evaluated) is { } pending)
        {
            return BugFixErrors.AttemptRefused.With($"candidate {pending.Id.Value} has not been evaluated yet");
        }

        if (state.Attempt > 0 && current.Count < Candidates(state.Mode))
        {
            return BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture,
                $"attempt {state.Attempt} (mode B) has {current.Count} of its 2 competing candidates; record and evaluate the second, or advance report --unresolved"));
        }

        if (state.Attempt >= BugFixState.MaxAttempts)
        {
            return BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture, $"attempt cap ({BugFixState.MaxAttempts}) reached; run advance report --unresolved"));
        }

        return mode == FixMode.B ? Competing(state) : Results.Ok();
    }

    // Mode B (competing implementers) is the escalation after a failed attempt, one implementer per open hypothesis.
    private static Result<Unit> Competing(BugFixState state)
    {
        if (state.Attempt == 0)
        {
            return BugFixErrors.AttemptRefused.With("mode B (competing implementers) is the escalation after a failed attempt");
        }

        var open = state.Investigation?.Hypotheses.Count(static h => h.Result != HypothesisResult.RuledOut) ?? 0;
        return open >= 2
            ? Results.Ok()
            : BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture, $"mode B needs at least 2 hypotheses not ruled out; the handoff has {open}"));
    }
}
