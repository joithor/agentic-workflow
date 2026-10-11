using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Refusals (spec §5.5, W5, W14): what a gate answer means, and what follows a refused transition. A refusal is never
/// a dead end: every one consumes its trigger and ends in a re-run, a park, an escalation or SND-WF-004. P3 unit (a).
/// </summary>
public static class RefusalRules
{
    /// <summary>
    /// What one gate's answer means (spec §5.5, W4, W5): pass; fail refuses; uncertain or below the threshold
    /// escalates for a top-rung gate and refuses otherwise; no answer (no runtime) refuses.
    /// </summary>
    /// <param name="use">The recipe's use of the gate (its threshold).</param>
    /// <param name="answer">The gate's answer; null when no runtime answered.</param>
    /// <param name="descriptor">The gate's descriptor (its rung).</param>
    /// <returns>The record the event carries.</returns>
    public static GateRecord Classify(GateUse use, GateResult? answer, BlockDescriptor descriptor) =>
        throw new NotSupportedException("P3 unit (a): RefusalRules.Classify");

    /// <summary>
    /// What follows a refused transition, in the §5.5 precedence: a 'gate.budget.*' refusal parks with budget
    /// (releasing a wait's held resources); a top-rung gate escalates; from an active state on a Step trigger the
    /// Step re-runs below the loop's cap, parks with rounds (kind refusal) at it, or parks with needs-answer without
    /// a loop; from a wait the trigger is withheld and it parks with needs-answer (kind wait); a command gets SND-WF-004.
    /// </summary>
    /// <param name="input">The refused transition and its records.</param>
    /// <returns>What follows.</returns>
    public static RefusalOutcome Refuse(RefusalInput input) => Refuse(input, EngineRules.Default);

    /// <summary><see cref="Refuse(RefusalInput)"/> through <paramref name="rules"/>.</summary>
    internal static RefusalOutcome Refuse(RefusalInput input, IEngineRules rules) =>
        throw new NotSupportedException("P3 unit (a): RefusalRules.Refuse");
}
