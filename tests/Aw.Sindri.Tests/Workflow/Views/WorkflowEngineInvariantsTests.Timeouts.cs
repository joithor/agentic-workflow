using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Invariant 6: every active or wait state serves the timeout and budget its recipe declares (WF 3.4, W20).</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_active_or_wait_state_serves_its_declared_timeout_and_budget_and_on_names_the_state() => Walk(Timeouts);

    private static List<string> Timeouts(View view, RecipeShape shape)
    {
        var state = StateOf(view, shape);
        return [.. view.Statuses.SelectMany(status => state.Kind == StateKind.Terminal ? Finished(status) : [.. TimeoutLine(view, state, status), .. BudgetLine(shape, state, status)])];
    }

    private static List<string> Finished(StatusFields status) =>
        status.Timeout is null && status.Budget is null ? [] : ["a finished instance serves a timeout or budget line"];

    private static List<string> TimeoutLine(View view, ShapeState state, StatusFields status)
    {
        if (view.EngineOwned is not null)
        {
            return [];
        }

        if (status.Timeout is not { } timeout || state.Served is not { } served)
        {
            return ["no timeout line (WF 3.4: every active and wait state has one)"];
        }

        var kind = served.Then == TimeoutAction.Escalate ? TimeoutKind.Escalates : TimeoutKind.Parks;
        var left = served.After.TotalSeconds - (WorkflowViewFixtures.Now - view.Since).TotalSeconds;
        return timeout.Kind == kind && Math.Abs(timeout.InSeconds - left) <= 1 && timeout.On == view.State
            ? []
            : [M($"timeout {timeout.Kind} in {timeout.InSeconds}s on '{timeout.On}', the declaration serves {kind} in {left}s on '{view.State}' (declared {state.Timeout!.After} {state.Timeout.Then}{(state.RunsAtDesk ? ", desk floor 2d" : string.Empty)})")];
    }

    private static List<string> BudgetLine(RecipeShape shape, ShapeState state, StatusFields status)
    {
        var served = status.Budget is { } budget && budget.Scope == "state" ? budget : null;
        if (state.BudgetTokens is not { } tokens)
        {
            return served is null ? [] : ["a state budget line, the state declares none"];
        }

        return served is not null && served.Limit == tokens && served.Instance.Limit == shape.BudgetTokens
            ? []
            : [M($"budget line {(served is null ? "missing" : served.Limit)}, the state declares {tokens} of {shape.BudgetTokens}")];
    }
}
