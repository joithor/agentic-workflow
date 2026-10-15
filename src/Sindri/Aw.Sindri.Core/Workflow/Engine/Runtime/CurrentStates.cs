using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The active state's step declaration, the gates on its exits and the desk form (API spec §3.8 'current').</summary>
internal static class CurrentStates
{
    /// <summary>The step: 'active' for a state that runs a Step block, 'wait' for one waiting on a child or a person; null for a terminal state.</summary>
    public static StepDeclaration? Step(WorkflowInstance instance, InterpretedRecipe interpreted)
    {
        if (!interpreted.Recipe.States.TryGetValue(instance.Frame.State, out var state))
        {
            return null;
        }

        return state switch
        {
            { Step: { } step } => new StepDeclaration("active", step.Use.Value, step.Use.Value),
            { WaitFor.Child: { } child } => new StepDeclaration("wait", child.Recipe.Value, "one child per packet task"),
            { WaitFor: not null } => new StepDeclaration("wait", instance.Frame.State, "waits for an answer"),
            _ => null,
        };
    }

    /// <summary>The gates on the transitions leaving the active state, once each.</summary>
    public static IReadOnlyList<GateLine> Gates(WorkflowInstance instance, InterpretedRecipe interpreted) =>
    [
        .. interpreted.Recipe.Transitions
            .Where(t => t.From.Contains(instance.Frame.State, StringComparer.Ordinal))
            .SelectMany(static t => t.Gates)
            .DistinctBy(static g => g.Use)
            .Select(static g => new GateLine(g.Use, "exit", g.Threshold, g.Threshold is { } at ? string.Create(CultureInfo.InvariantCulture, $"{g.Use.Value} passes at {at} or above.") : $"{g.Use.Value} passes.")),
    ];

    /// <summary>The desk form of the open task: its run, the outputs owed, the checks the engine runs on a report and the action that completes it.</summary>
    public static DeskForm? Desk(DeskTask? task, InterpretedRecipe interpreted) => task is null
        ? null
        : new DeskForm(
            task.Run,
            [.. task.Outputs.Select(o => new DeskOutput(o.Fact.Value, FactTypes.Parse(TypeOf(interpreted, o.Fact)), o.RequiredOn.Count > 0))],
            [.. task.Outputs.Where(o => DeskChecks.Applies(task, o)).Select(o => DeskChecks.Id(interpreted, o.Fact))],
            ActionId.From("CompleteStep"));

    private static string? TypeOf(InterpretedRecipe interpreted, FactKey fact) => interpreted.Recipe.Outputs.FirstOrDefault(r => r.Key == fact)?.Type ?? Recipes.FactTypes.Default;
}
