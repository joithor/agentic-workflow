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
    private const string InterviewAnswer = "step.interview.answer";

    /// <summary>The open interview desk run's token, or the refusal (SND-WF-003, "rewind to interview first") for an instance that isn't at it.</summary>
    /// <param name="instance">The instance as it stands.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The run token to complete as 'answered', or the refusal.</returns>
    public static Result<RunToken> InterviewRun(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var address = instance.Address.Value;
        var fix = $"rewind to interview first: sindri workflow rewind {address} --to interview";
        if (instance.Ended || instance.Suspension is not null)
        {
            return WorkflowErrors.TriggerNotAllowed.With($"answer --record needs {address} at 'interview' with its desk run open, and it is {(instance.Ended ? "ended" : "parked or taken over")} (fix: {fix})");
        }

        var step = recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.Step : null;
        return string.Equals(step?.Use.Value, InterviewAnswer, StringComparison.Ordinal) && instance.Frame.Run is { Runtime: StepRuntime.Desk } run
            ? run.Token
            : WorkflowErrors.TriggerNotAllowed.With($"answer --record needs {address} at 'interview', not '{instance.State}' (fix: {fix})");
    }
}
