using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Which recipe transition's gates a trigger runs (engine P7 plan D7): the caller evaluates exactly these before it decides.</summary>
public static class MatchedTransition
{
    /// <summary>
    /// The transition whose gates <paramref name="trigger"/> runs: the one it matches; for an answer the transition its park withheld;
    /// on an answer wait the wait's own transition. A trigger no gate guards (a deadline, a cancel, a start) has none.
    /// </summary>
    /// <param name="instance">The instance as it stands.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="trigger">The trigger about to be decided.</param>
    /// <returns>The transition, or null.</returns>
    public static RecipeTransition? Of(WorkflowInstance instance, Recipe recipe, Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(trigger);
        var index = trigger switch
        {
            StepCompleted or StepFailed or WaitSatisfied or CommandFired => TransitionDecision.Match(instance, recipe, trigger).Transition,
            AnswerGiven when instance.Suspension is { } suspension => suspension.Resume.Withheld?.Transition,
            AnswerGiven => TransitionDecision.Match(instance, recipe, trigger).Transition,
            _ => null,
        };
        return index is { } i && i >= 0 && i < recipe.Transitions.Count ? recipe.Transitions[i] : null;
    }
}
