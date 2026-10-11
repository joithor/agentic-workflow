using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Steps;

/// <summary>Which repair proposals may be tried: the judge only proposes; the candidate must be real and fit the action.</summary>
public static class RepairRules
{
    /// <summary>Repair attempts per broken step.</summary>
    public const int MaxAttempts = 2;

    private static readonly HashSet<string> _fillableRoles = new(StringComparer.Ordinal) { "input", "textbox", "textarea", "searchbox" };

    /// <summary>Only click and fill steps are ever repaired.</summary>
    /// <param name="action">The step's action.</param>
    /// <returns>Whether a broken step of this action may be repaired.</returns>
    public static bool IsRepairable(StepAction action) => action is StepAction.Click or StepAction.Fill;

    /// <summary>The chosen candidate, only when the proposal chose one that is in the visible list.</summary>
    /// <param name="proposal">The judge's proposal (null when the judge failed).</param>
    /// <param name="visible">The candidates offered.</param>
    /// <returns>The candidate, or null.</returns>
    public static Candidate? Resolve(SelectorRepair? proposal, IReadOnlyList<Candidate> visible) =>
        proposal is { Decision: RepairDecision.Repaired, ChosenIndex: { } index }
            ? visible.FirstOrDefault(c => c.Index == index)
            : null;

    /// <summary>
    /// A click lands anywhere; a fill must land on something fillable. Where writes are refused, a candidate that reads
    /// like a write control (<see cref="StepGuard.LooksLikeWrite(Candidate)"/>) never fits: the guard vetted the script's
    /// targets, and a repair must not reach past it.
    /// </summary>
    /// <param name="action">The step's action.</param>
    /// <param name="candidate">The candidate.</param>
    /// <param name="refuseWrites">Whether writes are refused on this host.</param>
    /// <returns>Whether the candidate fits.</returns>
    public static bool Fits(StepAction action, Candidate candidate, bool refuseWrites) =>
        !(refuseWrites && StepGuard.LooksLikeWrite(candidate))
        && (action == StepAction.Click || (action == StepAction.Fill && candidate.Role is { } role && _fillableRoles.Contains(role)));
}
