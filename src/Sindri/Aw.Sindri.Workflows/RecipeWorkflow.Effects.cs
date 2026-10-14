using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's effects (engine spec rev 8 §5.7, §3.4; engine P6). Not wired into the loop yet: unit (a) calls
/// <see cref="Dispatch"/> from 'Apply' after the transition's ledger event, as a workflow task the loop doesn't await,
/// so the instance enters its next state while the effect waits out the delay.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>
    /// Runs <paramref name="effects"/> of the change that left the instance as <paramref name="after"/>, each through
    /// 'RecordEffect' (pending), 'Workflow.DelayAsync(workflow.dispatchDelay)' (cancelled if the instance is),
    /// 'RecheckEffect', then 'RunEffect' or the refusal ('RecordEffect' refused, then 'Submit(OutboxRefused)'; unit (b)).
    /// Engine-origin notices (unit (c), 'EngineNotices.For') take the same path, exempt from the recheck.
    /// </summary>
    /// <param name="after">The instance after the change.</param>
    /// <param name="effects">The effects to run.</param>
    /// <returns>Done once every effect is dispatched or refused.</returns>
    internal Task Dispatch(WorkflowInstance after, IReadOnlyList<EffectCommand> effects)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(effects);
        throw new NotSupportedException($"engine P6 unit (a): the effect dispatch of {_start?.WorkflowId().Value ?? "no instance"} is not built yet");
    }
}
