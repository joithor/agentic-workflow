using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// 'workflow answer --record' (#1270): a desk-free answer batch goes in through the open run of the 'step.interview.answer' desk Step, so
/// 'step.interview.record' runs as normal. This is the rule that finds that run, or refuses with the fix.
/// </summary>
public static class AnswerRecording
{
    /// <summary>The open interview desk run's token, or the refusal (SND-WF-003, "rewind to interview first") for an instance that isn't at it.</summary>
    /// <param name="instance">The instance as it stands.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The run token to complete as 'answered', or the refusal.</returns>
    public static Result<RunToken> InterviewRun(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        throw new NotSupportedException("answer --record is not implemented yet (#1270)");
    }
}
