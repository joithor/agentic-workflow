using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// An answer wait (spec §3.4, §5.8): with nothing suspended, 'workflow answer --text' in a state that waits for an
/// answer is 'wait.satisfied' with outcome 'answered'. It records 'fact:answer' and is decided as a recipe trigger
/// under the answer's own key, with the answer as the event's trigger; any other variant is SND-WF-003 naming '--text'.
/// </summary>
public static partial class TransitionDecision
{
    private static readonly OutcomeName _answered = OutcomeName.From("answered");

    /// <summary>The answer wait of the instance's current state, when nothing is suspended over it.</summary>
    private static AnswerWait? WaitingForAnswer(WorkflowInstance instance, Recipe recipe) =>
        instance.Suspension is null && !instance.Ended && recipe.States.TryGetValue(instance.State, out var state)
            ? state.WaitFor?.Answer
            : null;

    /// <summary>A text answer on an answer wait, as the 'wait.satisfied' it stands for; null for any other trigger or state.</summary>
    private static WaitSatisfied? AnswerSatisfies(WorkflowInstance instance, Recipe recipe, Trigger trigger) =>
        trigger is AnswerGiven { Variant: AnswerVariant.Text, Text: { } text } answer && WaitingForAnswer(instance, recipe) is { } wait
            ? new WaitSatisfied(answer.Actor, new AnswerWaitSource(wait.Id), _answered,
                new Dictionary<FactKey, FactValue> { [_answer] = AnswerFact(text) })
            : null;

    private static Decision AnswerTheWait(Deciding d, WorkflowInstance instance, AnswerGiven answer, AnswerWait wait)
    {
        if (AnswerSatisfies(instance, d.Recipe, answer) is not { } satisfied)
        {
            var address = instance.Address.Value;
            return new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"{address} waits for an answer to '{wait.Question}', which only --text gives (fix: sindri workflow answer {address} --text <answer>)"));
        }

        return DecideRecipeTrigger(d.Withholding(satisfied), WithAnswer(instance, answer));
    }
}
