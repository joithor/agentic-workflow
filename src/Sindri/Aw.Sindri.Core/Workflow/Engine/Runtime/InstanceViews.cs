using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Builds the 'GetInstance' view from the interpreter's state (engine spec rev 8 §5.1, §6.4). Pure, so the Query and
/// its tests call the same code. It carries the open desk task with the Step's declared outcomes and outputs,
/// 'allowedCommands', and the rendered words status prints: the header's detail, 'next' (§3.8, §3.7), the answer line
/// per park reason, a drift waiting behind a park, and the token budgets (engine P4 unit (e)).
/// </summary>
public static class InstanceViews
{
    /// <summary>The view of <paramref name="instance"/>, with every layout member filled: item, urgency, engine badge, subject, inputs, state cells, fact rows and the status record.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs, its blocks' descriptors and the input facts.</param>
    /// <param name="since">When it entered its state.</param>
    /// <param name="timeoutAt">When the state's timeout fires, if it has one armed.</param>
    /// <param name="context">The time it is as of, the version, the live children and the facts' provenance.</param>
    /// <returns>The view.</returns>
    public static InstanceView Of(
        WorkflowInstance instance,
        InterpretedRecipe recipe,
        DateTimeOffset since,
        DateTimeOffset? timeoutAt,
        InstanceViewContext context)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(context);
        var children = context.Children ?? [];
        var task = instance is { Ended: false, Suspension: null, Frame.Run: { Runtime: StepRuntime.Desk } open }
            ? Task(instance.Frame.State, open.Token, recipe, timeoutAt)
            : null;
        var words = new InstanceWords(instance, recipe, since, children.Count > 0 ? children[0] : null);
        var view = new InstanceView(
            instance.Address,
            recipe.Version,
            instance.State,
            task is not null ? "desk task" : words.Detail(),
            since,
            words.Next(task),
            Allowed(instance, task),
            instance.Suspension is { } s ? new OpenQuestion(s.Kind, s.Reason, s.Question) : null,
            task,
            instance.Frame.Run?.Token,
            children,
            instance.Outcome)
        {
            Id = WorkflowInstanceId.Of(instance.Address),
            Version = context.Version,
            Answer = words.Answer(),
            Drift = words.Drift(),
            Budget = Budget(instance, recipe),
            RunningSteps = instance is { Ended: false, Frame.Run.Runtime: not StepRuntime.Desk } ? 1 : 0,
            Paused = instance.Suspension is { Kind: SuspensionKind.Takeover, Remaining: { } paused } ? paused.Left : null,
        };
        return WithLayout(view, new Layout(instance, recipe, task, words), context);
    }

    /// <summary>The API's detail view of <paramref name="view"/>: a pure function of the view and the context (no clock, no I/O).</summary>
    /// <param name="view">The instance view <see cref="Of(WorkflowInstance, InterpretedRecipe, DateTimeOffset, DateTimeOffset?, InstanceViewContext)"/> built.</param>
    /// <param name="context">The title, parent, counts and as-of time the read service supplies.</param>
    /// <returns>The detail the API serves.</returns>
    public static InstanceDetail Detail(InstanceView view, InstanceDetailContext context)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(context);
        var status = view.Status ?? throw new ArgumentException("the view has no status record; build it with InstanceViews.Of", nameof(view));
        return new InstanceDetail(
            view.Address,
            view.Id,
            context.Parent,
            view.ItemId,
            context.Title,
            new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, view.Recipe.Version, view.Urgency),
            view.State,
            view.Since,
            view.EngineOwned,
            view.Subject ?? InstanceSummaries.NoSubject(view),
            view.OrderedBy,
            view.States,
            new CurrentState(view.State, view.Step, view.Gates, view.Desk, status),
            view.Facts,
            view.ChildGroups,
            context.Counts,
            [.. status.Commands.Select(static c => c.ActionId)],
            context.AsOf);
    }

    /// <summary>The state the layout members are built from.</summary>
    private sealed record Layout(WorkflowInstance Instance, InterpretedRecipe Recipe, DeskTask? Task, InstanceWords Words);

    /// <summary>The layout members (UI screens E-20, E-23), each from the interpreted recipe, the instance and its facts; the status record last, since it renders the rest.</summary>
    private static InstanceView WithLayout(InstanceView view, Layout from, InstanceViewContext context)
    {
        var badge = from.Words.EngineBadge();
        var complete = view with
        {
            ItemId = from.Instance.Item,
            Urgency = RecipeUrgencies.UrgencyOf(from.Recipe),
            EngineOwned = badge,
            Subject = SubjectRefs.Of(from.Recipe),
            Inputs = InputSlots.Of(from.Instance, from.Recipe),
            OrderedBy = StatePathOrder.Of(from.Recipe.Recipe).OrderedBy == "transition-list" ? PathOrdering.TransitionList : PathOrdering.Path,
            States = StateCells.Of(from.Instance, from.Recipe, badge),
            Facts = FactRows.Of(from.Instance, from.Recipe, from.Task, context.Provenance ?? new Dictionary<FactKey, FactProvenance>()),
            ChildGroups = [],
            Step = CurrentStates.Step(from.Instance, from.Recipe),
            Gates = CurrentStates.Gates(from.Instance, from.Recipe),
            Desk = CurrentStates.Desk(from.Task, from.Recipe),
        };
        return complete with { Status = StatusRecords.Of(complete, from.Words, context.Now) };
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
            commands.Add(new("CompleteStep", CompleteCli(address, task)));
        }

        commands.Add(instance.Suspension is { Kind: SuspensionKind.Takeover }
            ? new("ApplyCommand:handback", $"sindri workflow handback {address}")
            : new("ApplyCommand:takeover", $"sindri workflow takeover {address}"));
        commands.Add(new("ApplyCommand:cancel", $"sindri workflow cancel {address}"));
        return commands;
    }

    /// <summary>
    /// The complete command with the outcome it reports (the Step's first declared) and an '--output &lt;fact&gt;=&lt;file&gt;' flag for each
    /// output required on that outcome (API spec §3.8 'desk runtime'); '&lt;outcome&gt;' when the Step declares none.
    /// </summary>
    private static string CompleteCli(string address, DeskTask task)
    {
        var line = new StringBuilder($"sindri workflow complete {address} --run {task.Run.Value} --outcome ");
        if (task.Outcomes.Count == 0)
        {
            return line.Append("<outcome>").ToString();
        }

        _ = line.Append(task.Outcomes[0].Value);
        foreach (var output in task.Outputs.Where(o => o.RequiredOn.Contains(task.Outcomes[0])))
        {
            _ = line.Append(" --output ").Append(output.Fact.Value).Append("=<file>");
        }

        return line.ToString();
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
