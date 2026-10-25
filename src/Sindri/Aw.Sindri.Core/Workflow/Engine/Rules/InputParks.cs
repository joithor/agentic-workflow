using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The park for a Step input whose runtime is gone at Fill, after the recipe was approved on a build that had it (engine
/// decision 2026-10-17): the existing 'no runtime' park, which shows SND-WF-010, with the question naming SND-WF-016, the
/// Step, the input and the fix. A restart runs Fill again; a runtime still missing parks it again the same way.
/// </summary>
public static class InputParks
{
    /// <summary>The question the park asks.</summary>
    /// <param name="input">The input without a runtime.</param>
    /// <param name="step">The Step it fills for.</param>
    /// <returns>The sentence.</returns>
    public static string Question(BlockId input, BlockId step) =>
        $"input {input.Value} of {step.Value} has no runtime in this build ({WorkflowErrors.StepInputNoRuntime.Code}; fix: install the block pack or rebuild, then restart)";

    /// <summary>Parks <paramref name="instance"/> for the missing runtime of <paramref name="input"/>.</summary>
    /// <param name="instance">The instance whose Step was about to run.</param>
    /// <param name="input">The input without a runtime.</param>
    /// <param name="step">The Step it fills for.</param>
    /// <param name="actor">Who parked it.</param>
    /// <returns>The parked instance.</returns>
    public static WorkflowInstance Park(WorkflowInstance instance, BlockId input, BlockId step, string actor) =>
        Suspensions.Suspend(instance, new Suspension(
            SuspensionKind.Park, ParkReason.NoRuntime, ParkKind: null, Escalation: null, Question(input, step), actor, Mirrors: null,
            new ResumeAction(ResumeKind.Restart, Withheld: null, Gate: null, Effects: [])));
}
