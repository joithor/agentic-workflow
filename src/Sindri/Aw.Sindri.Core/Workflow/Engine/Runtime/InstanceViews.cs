using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Builds the 'GetInstance' view from the interpreter's state (engine spec rev 8 §5.1, §6.4). Pure, so the Query and
/// its tests call the same code. It carries the open desk task with the Step's declared outcomes and outputs,
/// 'allowedCommands', and the rendered words status prints: the header's detail, 'next' (§3.8, §3.7), the answer line
/// per park reason, a drift waiting behind a park, and the token budgets (engine P4 unit (e)).
/// </summary>
public static class InstanceViews
{
    /// <summary>The view of <paramref name="instance"/>, with no children (P7 adds them).</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs, its blocks' descriptors and the input facts.</param>
    /// <param name="since">When it entered its state.</param>
    /// <param name="timeoutAt">When the state's timeout fires, if it has one armed.</param>
    /// <returns>The view.</returns>
    public static InstanceView Of(
        WorkflowInstance instance,
        InterpretedRecipe recipe,
        DateTimeOffset since,
        DateTimeOffset? timeoutAt) => Of(instance, recipe, since, timeoutAt, []);

    /// <summary>The view of <paramref name="instance"/> and its live children (a park or escalation mirroring a child names the first).</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs, its blocks' descriptors and the input facts.</param>
    /// <param name="since">When it entered its state.</param>
    /// <param name="timeoutAt">When the state's timeout fires, if it has one armed.</param>
    /// <param name="children">The live children.</param>
    /// <returns>The view.</returns>
    public static InstanceView Of(
        WorkflowInstance instance,
        InterpretedRecipe recipe,
        DateTimeOffset since,
        DateTimeOffset? timeoutAt,
        IReadOnlyList<ChildView> children)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(children);
        var task = instance is { Ended: false, Suspension: null, Frame.Run: { Runtime: StepRuntime.Desk } open }
            ? Task(instance.Frame.State, open.Token, recipe, timeoutAt)
            : null;
        var question = instance.Suspension is { } s ? new OpenQuestion(s.Kind, s.Reason, s.Question) : null;
        var words = new InstanceWords(instance, recipe, since, children.Count > 0 ? children[0] : null);
        return new InstanceView(
            instance.Address,
            recipe.Version,
            instance.State,
            task is not null ? "desk task" : words.Detail(),
            since,
            words.Next(task),
            Allowed(instance, task),
            question,
            task,
            instance.Frame.Run?.Token,
            children,
            instance.Outcome)
        {
            Answer = words.Answer(),
            Drift = words.Drift(),
            Budget = Budget(instance, recipe),
            Paused = instance.Suspension is { Kind: SuspensionKind.Takeover, Remaining: { } paused } ? paused.Left : null,
        };
    }

    /// <summary>
    /// The commands the instance accepts now, its own verbs first in the order 'answer', 'complete', 'handback',
    /// 'takeover', 'cancel' (§6.4). An ended instance accepts none. A park whose only way out is an approval or a
    /// cancel (spec-conflict: revoked, child revoked, drift; §3.8) takes no answer.
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
        if (instance.Suspension is { Kind: not SuspensionKind.Takeover } open && Answerable(open))
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

    private static bool Answerable(Suspension suspension) =>
        suspension.ParkKind is not (ParkKind.Revoked or ParkKind.ChildRevoked or ParkKind.Drift);

    private static DeskTask Task(string state, RunToken run, InterpretedRecipe recipe, DateTimeOffset? timeoutAt)
    {
        var step = recipe.Recipe.States[state].Step?.Use
            ?? throw new InvalidOperationException($"state {state} has an open run but no Step");
        var descriptor = recipe.Catalog.Find(step);
        return new DeskTask(run, state, step, descriptor?.Outcomes ?? [], descriptor?.Outputs ?? [], recipe.Inputs, timeoutAt);
    }

    /// <summary>The budgets of the active state and the instance, when either is declared and the instance hasn't ended.</summary>
    private static BudgetView? Budget(WorkflowInstance instance, InterpretedRecipe recipe)
    {
        var stateCap = recipe.Recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.Budget?.Tokens : null;
        var instanceCap = recipe.Recipe.Budget?.Tokens;
        if (instance.Ended || (stateCap is null && instanceCap is null))
        {
            return null;
        }

        var spentHere = instance.Spent.ByState.TryGetValue(instance.Frame.State, out var spent) ? spent : 0;
        return new BudgetView(spentHere, stateCap, instance.Spent.Total, instanceCap);
    }
}
