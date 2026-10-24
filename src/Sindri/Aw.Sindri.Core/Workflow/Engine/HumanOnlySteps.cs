using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// Which Steps only a human may report (DT spec §8.5, scoping-station spec W3): a Step whose descriptor says
/// <see cref="BlockDescriptor.HumanOnly"/>. A block-runtime Step on the desk is answerable by any caller (owner q93, 2026-10-23). Pure.
/// </summary>
public static class HumanOnlySteps
{
    /// <summary>Whether a report on <paramref name="step"/> needs a human caller when the run is on <paramref name="runOn"/>.</summary>
    /// <param name="step">The Step's descriptor.</param>
    /// <param name="runOn">Who runs the Step now.</param>
    /// <returns>True when only a human may complete it.</returns>
    public static bool Applies(BlockDescriptor step, StepRuntime runOn)
    {
        ArgumentNullException.ThrowIfNull(step);
        return runOn == StepRuntime.Desk && step.HumanOnly == true;
    }
}
