using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Builds the 'GetInstance' view from the interpreter's state (engine spec rev 8 §5.1, §6.4). Pure, so the Query and
/// its tests call the same code. Engine P4 contract: the open desk task with the Step's declared outcomes and outputs,
/// and 'allowedCommands' for a desk task, a suspension and an ended instance; unit (e) renders 'next' and the
/// per-reason answer lines (§6.4), unit (a) the wait states, P7 the children.
/// </summary>
public static class InstanceViews
{
    /// <summary>The view of <paramref name="instance"/>.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs, its blocks' descriptors and the input facts.</param>
    /// <param name="since">When it entered its state.</param>
    /// <param name="timeoutAt">When the state's timeout fires, if it has one armed.</param>
    /// <returns>The view.</returns>
    public static InstanceView Of(
        WorkflowInstance instance,
        InterpretedRecipe recipe,
        DateTimeOffset since,
        DateTimeOffset? timeoutAt)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var task = instance is { Ended: false, Suspension: null, Frame.Run: { Runtime: StepRuntime.Desk } open }
            ? Task(instance.Frame.State, open.Token, recipe, timeoutAt)
            : null;
        var question = instance.Suspension is { } s ? new OpenQuestion(s.Kind, s.Reason, s.Question) : null;
        return new InstanceView(
            instance.Address,
            recipe.Version,
            instance.State,
            task is not null ? "desk task" : null,
            since,
            Next: string.Empty,
            Allowed(instance, task),
            question,
            task,
            instance.Frame.Run?.Token,
            Children: [],
            instance.Outcome);
    }

    /// <summary>
    /// The commands the instance accepts now, its own verbs first in the order 'answer', 'complete', 'handback',
    /// 'takeover', 'cancel' (§6.4). An ended instance accepts none.
    /// </summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="task">The open desk task, if any.</param>
    /// <returns>Each command's action id and CLI line.</returns>
    public static IReadOnlyList<AllowedCommand> Allowed(WorkflowInstance instance, DeskTask? task)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Ended)
        {
            return [];
        }

        var address = instance.Address.Value;
        var commands = new List<AllowedCommand>();
        if (instance.Suspension is { Kind: not SuspensionKind.Takeover })
        {
            commands.Add(new("ApplyCommand:answer", $"sindri workflow answer {address}"));
        }

        if (task is not null)
        {
            commands.Add(new("CompleteStep", $"sindri workflow complete {address} --run {task.Run.Value} --outcome <outcome>"));
        }

        commands.Add(instance.Suspension is { Kind: SuspensionKind.Takeover }
            ? new("ApplyCommand:handback", $"sindri workflow handback {address}")
            : new("ApplyCommand:takeover", $"sindri workflow takeover {address}"));
        commands.Add(new("ApplyCommand:cancel", $"sindri workflow cancel {address}"));
        return commands;
    }

    private static DeskTask Task(string state, RunToken run, InterpretedRecipe recipe, DateTimeOffset? timeoutAt)
    {
        var step = recipe.Recipe.States[state].Step?.Use
            ?? throw new InvalidOperationException($"state {state} has an open run but no Step");
        var descriptor = recipe.Catalog.Find(step);
        return new DeskTask(run, state, step, descriptor?.Outcomes ?? [], descriptor?.Outputs ?? [], recipe.Inputs, timeoutAt);
    }
}
