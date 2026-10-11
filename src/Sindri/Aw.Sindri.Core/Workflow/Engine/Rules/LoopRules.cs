using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Loops and round grants (spec §3.7, §5.5, §5.8, W7, A §17.23.13): rounds counted on the transition back into a
/// looped state, no-progress between rounds, and '--grant n'. P3 unit (c).
/// </summary>
public static class LoopRules
{
    /// <summary>
    /// Entering a looped state again (spec §3.7): the next round, or a park with no-progress (the round repeats the
    /// last one's mark on a signal the loop names) or rounds (past the allowed rounds), kind loop, resume transition.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The looped state being entered again.</param>
    /// <param name="mark">What the round that ends produced.</param>
    /// <returns>Go on, or park.</returns>
    public static LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.Reenter");

    /// <summary>Whether the loop of <paramref name="state"/> is at its allowed rounds (a refusal there parks with rounds, kind refusal).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The active state.</param>
    /// <returns>True at the cap; false below it or without a loop.</returns>
    public static bool AtCap(WorkflowInstance instance, Recipe recipe, string state) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.AtCap");

    /// <summary>Whether <paramref name="current"/> made no progress over <paramref name="previous"/> on a signal <paramref name="loop"/> names.</summary>
    /// <param name="loop">The loop.</param>
    /// <param name="previous">The last round's mark; null for the first round.</param>
    /// <param name="current">This round's mark.</param>
    /// <returns>True when a named signal repeats.</returns>
    public static bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.NoProgress");

    /// <summary>'--grant n' (W7): 1 ≤ n ≤ the loop's cap, on a rounds or no-progress park; the round it starts is the first of the n.</summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="rounds">n.</param>
    /// <returns>The instance with the allowed rounds raised, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> Grant(WorkflowInstance instance, Recipe recipe, int rounds) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.Grant");

    /// <summary>
    /// '--text' on a no-progress park (spec §3.8): the same as a grant (the resume performs the withheld transition or
    /// re-runs the Step) without adding rounds beyond the one it starts, once per loop.
    /// </summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The instance with the loop ready for the round the answer starts, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> AnswerText(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.AnswerText");

    /// <summary>The 'next' variables of every loop the recipe names (spec §3.7): '{loop.&lt;n&gt;}' (the round) and '{loop.&lt;n&gt;.cap}' (the last round allowed).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The values, by variable name without braces.</returns>
    public static IReadOnlyDictionary<string, string> Values(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("P3 unit (c): LoopRules.Values");
}
