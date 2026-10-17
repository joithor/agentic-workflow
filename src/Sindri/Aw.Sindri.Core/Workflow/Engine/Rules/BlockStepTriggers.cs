using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The triggers a block-runtime Step's result becomes (DT spec §8.2): the engine reports for the Step, and a result no desk report could have been is a failure, not a refusal.</summary>
public static class BlockStepTriggers
{
    /// <summary>The actor a block-runtime Step reports as.</summary>
    /// <param name="step">The Step's id.</param>
    /// <returns>'service:block:&lt;step&gt;'.</returns>
    public static string Actor(BlockId step) => throw new NotSupportedException();

    /// <summary>
    /// A completed result as a trigger: a <see cref="StepCompleted"/> when the report validation of WF §5.3 accepts it, else a
    /// <see cref="StepFailed"/> with the reason 'invalid-result: &lt;code&gt;' so the run can't hang on a refusal nobody sees.
    /// </summary>
    /// <param name="instance">The instance with the run open.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="context">The runtimes and the catalog.</param>
    /// <param name="result">What the Step reported.</param>
    /// <returns>The trigger to decide.</returns>
    public static Trigger Completed(WorkflowInstance instance, Recipe recipe, DecisionContext context, BlockCompletion result) =>
        throw new NotSupportedException();

    /// <summary>A failed run as a trigger.</summary>
    /// <param name="step">The Step.</param>
    /// <param name="run">The open run.</param>
    /// <param name="reason">What failed.</param>
    /// <returns>The step.failed trigger.</returns>
    public static StepFailed Failed(BlockId step, RunToken run, string reason) => throw new NotSupportedException();
}
